// -- plot related code 229 lines
module Plot

open ETData
open OxyPlot
open ControlTheorie

type gazePlotData =
    {
        targetNr: int
        target: Target
        targetLabel: string
        currNr: int
        currSize: int
        isError: int
        data: EvaluationData seq
        xZoom: Range<float> option
        saccs: vec2 list
        saccsData: (SaccadeEval) seq
        config: string list
        timeComments: (float*string)[]
        intervals: IntervalData seq
        targetStart:float
        blinkIntervals: IntervalData seq
        userCorr:UserCorrectionData
        analysis: EventAnalysisData list
    }

// -- main plot function, creates all data series from the analyzed data
let updateGazeModel (model:OxyPlot.PlotModel) (p: gazePlotData) =

    //printfn "updateGazeModel"

    model.Series.Clear() 
    model.Axes.Clear()
    model.Annotations.Clear()

    let spdScale = 1.0 / 25.0

    let validSpeed = 
        p.data
        |> Seq.filter (fun x -> not <| System.Double.IsNaN x.Speed)
        |> Array.ofSeq

    let reduce (defaultValue) (reduce) =
         match Array.isEmpty validSpeed with
         | true -> defaultValue
         | false -> reduce validSpeed

    let avgSpeed = reduce 0.0 (Array.averageBy (fun x -> x.Speed))
    let maxSpeed = reduce EvaluationData.Zero (Array.maxBy (fun x -> x.Speed))
    let minSpeed = reduce EvaluationData.Zero (Array.minBy (fun x -> x.Speed))
  
    let reactionTimeStamps = p.userCorr.reactionTime.TryFind p.currNr

    let reactionTime = 
        if reactionTimeStamps.IsSome then 
            reactionTimeStamps.Value.y - reactionTimeStamps.Value.x
        else
            p.analysis.Head.reactionTime

    model.Title <-
        match p.isError with
        | 9 -> sprintf "BadData --- '%s' %d/%d @ %d" p.target.FullName (p.currNr+1) (p.currSize) p.targetNr
        | 0 ->
            let sTime = ((Seq.head p.data).Time.TimeStamp) / 1000.0
            let eTime = ((Seq.last p.data).Time.TimeStamp) / 1000.0
            sprintf "'%s' %d/%d @ %d %.3f-%.3f [s], Speed avg: %.2f, ReactionTime: %.2f [ms], ClosestMood: %s" 
                       p.target.FullName (p.currNr+1) (p.currSize) p.targetNr sTime eTime avgSpeed reactionTime p.targetLabel

        | x -> sprintf "Error %d --- '%s' %d/%d @ %d" x p.target.FullName (p.currNr+1) (p.currSize) p.targetNr

    model.TitleFontSize <- 14.0

    // -- mark error flagged event by outer area in red
    model.Background <-
        match p.isError, p.analysis.Head.error with
        | 9, _
        | _, 10
            -> OxyColors.DarkGray
        | 0, _ when reactionTimeStamps.IsSome
            -> OxyColor.FromRgb( 220uy, 220uy, 255uy )
        | 0, _
            -> OxyColor.FromRgb( 245uy, 245uy, 250uy )
        | x, _
            -> OxyColor.FromRgb( 255uy, byte (210 + x * 5), 200uy )

    // -- mark error flagged event by outer area in blue
    model.PlotAreaBackground <- OxyColors.White 

    if p.analysis.Head.error < 10 then
        // -- reformat data to DataPoint required for OxyPlot data series

        /// add given points as series to model and return the points as array
        let (<**) (points) (conf:string, series:Series.DataPointSeries)  =
            let points = Array.ofSeq points
            if p.config |> List.contains conf then
                series.Points.AddRange(points)
                model.Series.Add(series)
            points

        let dataSpeedExtreme = 
            [|
                DataPoint(float minSpeed.Time.TimeStamp, minSpeed.Speed * spdScale)
                DataPoint(float maxSpeed.Time.TimeStamp, maxSpeed.Speed * spdScale)
            |]

            <** ("Speed", Series.StemSeries( 
                    Title = "SpeedMinMax", 
                    Color = OxyColors.Gray,
                    MarkerType = MarkerType.Diamond,
                    MarkerStroke = OxyColors.Black,
                    MarkerStrokeThickness = 2.0,
                    MarkerSize = 5.0
            ) ) 

        let dataSpeed =
            p.data
            |> Seq.map (fun x -> DataPoint(float x.Time.TimeStamp, x.Speed * spdScale))

            <** ("Speed", Series.StemSeries( Title = "Speed", Color = OxyColors.LightGray ) )

        let dataSpeedBeforeReact =
            dataSpeed
            |> Seq.filter( fun x -> 
                if reactionTimeStamps.IsSome then
                    x.X >= reactionTimeStamps.Value.x
                    && x.X <= reactionTimeStamps.Value.y
                else
                    x.X < p.targetStart + reactionTime
                    //(Seq.head intervals).tStart.x + reactionTime
            )
            
            <** ("Speed", Series.StemSeries( 
                    Title = "Speed-BeforeReaction",
                    Color = OxyColors.Black
            ) )

        let dataXError =
            p.data
            |> Seq.filter( fun x -> x.Valid )
            |> Seq.map( fun x -> DataPoint(float x.Time.TimeStamp, x.XError) )

            <** ("XError", Series.LineSeries( 
                    Title = "XError", 
                    Color = OxyColors.DarkBlue
            ) )

        let dataYError =
            p.data
            |> Seq.filter( fun x -> x.Valid )
            |> Seq.map( fun x -> DataPoint(float x.Time.TimeStamp, x.YError) )

            <** ("YError", Series.LineSeries( 
                    Title = "YError",
                    Color = OxyColors.DarkOrange,
                    StrokeThickness = 1.0
                    //LineStyle=LineStyle.LongDash,
            ) )

        let dataPupilSize =
            p.data
            |> Seq.filter( fun x -> x.Valid )
            |> Seq.map( fun x -> DataPoint(float x.Time.TimeStamp, x.PupilSizeMean ))

            <** ("PupilSize", Series.LineSeries( 
                    Title = "PupilSize", 
                    Color = OxyColors.DarkMagenta
            ) )

        let euclidean x y = sqrt (x*x + y*y)
    
        let DataEuclideanError =
            p.data
            |> Seq.map( fun x -> DataPoint(float x.Time.TimeStamp, euclidean x.XError x.YError ) )

            <** ("EError", Series.LineSeries( 
                    Title = "EError",
                    Color = OxyColors.DarkRed,
                    StrokeThickness = 1.0
                    //LineStyle=LineStyle.LongDash,
            ) )

        // -- add data series and a legend to the plot model

        //model.LegendTitle <- "Legend"
        //model.LegendPosition <- LegendPosition.RightTop

        let maxDisplay =
            [ 
                dataXError |> Seq.filter (fun x -> x.Y = x.Y) |> Seq.map (fun x -> x.Y) |> Seq.max
                dataYError |> Seq.filter (fun x -> x.Y = x.Y) |> Seq.map (fun x -> x.Y) |> Seq.max
                maxSpeed.Speed * spdScale
            ]
            |> Seq.max

        let minDisplay =
            [ 
                dataXError |> Seq.filter (fun x -> x.Y = x.Y) |> Seq.map (fun x -> x.Y) |> Seq.min
                dataYError |> Seq.filter (fun x -> x.Y = x.Y) |> Seq.map (fun x -> x.Y) |> Seq.min
                minSpeed.Speed * spdScale
                -maxDisplay / 10.0
            ]
            |> Seq.min

        // -- interval regression
        let dataRegression =
            p.intervals
            |> Seq.collect( fun x -> 
                [
                    DataPoint(x.tStart.x , x.tStart.x * x.reg.k + x.reg.d)
                    DataPoint(x.tEnd.x   , x.tEnd.x   * x.reg.k + x.reg.d) 
                ])

            <** ("LRegression", Series.LineSeries( 
                    Title = "LRegression",
                    Color = OxyColors.Black,
                    StrokeThickness = 1.0,
                    LineStyle = LineStyle.Dash
            ) )

        // -- saccade regression
        let saccRegression =
            p.saccsData
            |> Seq.mapi( fun i s ->
                s.Fitted
                |> Seq.map( fun x -> DataPoint(x.x , x.y) )

                <** ("NLRegression", Series.LineSeries( 
                        Title = sprintf "NLRegression-%d" i,
                        Color = OxyColors.DarkMagenta,
                        StrokeThickness = 4.0,
                        LineStyle = LineStyle.Dash
                ) ) )

        let fullAngleError=
            p.saccsData
            |> Seq.mapi( fun i s ->
                s.Fitted
                |> Seq.zip s.FullAngles
                |> Seq.map( fun (x,y) -> DataPoint(y.x, x) )
          
                <** ("AFullError", Series.LineSeries( 
                        Title = sprintf "AFullError-%d" i,
                        Color = OxyColors.DarkBlue,
                        StrokeThickness = 1.0,
                        LineStyle = LineStyle.Dash
                ) ) )

        let inAngleError=
            p.saccsData
            |> Seq.mapi( fun i s ->
                s.Fitted
                |> Seq.zip s.AxisAngles
                |> Seq.map( fun (x,y) -> DataPoint(y.x, x.x) )
          
                <** ("AInError", Series.LineSeries( 
                        Title = sprintf "AInError-%d" i,
                        Color = OxyColors.DarkRed,
                        StrokeThickness = 1.0,
                        LineStyle = LineStyle.Dash
                ) ) )

        let fromAngleError=
            p.saccsData
            |> Seq.mapi( fun i s ->
                //printfn "%d =? %d" (Seq.length s.Fitted) (Seq.length s.AxisAngles)
                s.Fitted
                |> Seq.zip s.AxisAngles
                |> Seq.map( fun (x,y) -> DataPoint(y.x, x.y) )
          
                <** ("AFromError", Series.LineSeries( 
                        Title = sprintf "AFromError-%d" i,
                        Color = OxyColors.DarkRed,
                        StrokeThickness = 1.0,
                        LineStyle = LineStyle.Dash
                ) ) )

        let hView =
            p.saccsData
            |> Seq.mapi( fun i s ->
                s.Fitted
                |> Seq.mapi( fun j x -> 
                    if j < s.Fitted.Length-1 then
                        DataPoint( x.x , s.Fitted.[j+1].x - x.x ) 
                    else
                        DataPoint( x.x , 0.0 )  )

                <** ("NLRegression", Series.StemSeries( 
                        Title = sprintf "h-%d" i,
                        Color = OxyColor.FromRgb(255uy, 220uy, 255uy) ) ) )

        //printfn "# HView: %d"   (Seq.length hView)
        //printfn "# Saccs: %d"   (Seq.length saccRegression)
        //printfn "# AfuErrors: %d" (Seq.length fullAngleError)
        //printfn "# AinErrors: %d" (Seq.length inAngleError)
        //printfn "# AfrErrors: %d" (Seq.length fromAngleError)

        // -- interval annotations
//        let yBot = minDisplay
//        let yTop1 = minDisplay + (maxDisplay - minDisplay) / 20.0
//        let yTop2 = minDisplay + (maxDisplay - minDisplay) / 10.0

        let yTop2    = 0.0
        let yTop1    = yTop2 - 2.0
        let yBot     = yTop1 - 4.0
        let yBotBot  = yBot - 4.0
        


        if p.config |> List.contains "Intervals" then
            //printfn "Plotting intervals"
            let intervalColor e =
                match e with 
                | EyeEvent.Fixation    -> OxyColors.LightGreen
                | EyeEvent.ProSaccade  -> OxyColors.LightBlue
                | EyeEvent.AntiSaccade -> OxyColors.LightGoldenrodYellow
                | EyeEvent.BadData     -> OxyColors.LightGray
                | EyeEvent.Blink       -> OxyColors.Black
                | EyeEvent.Unknown _ ->   OxyColors.Red

            if (Seq.length p.intervals) > 0 then 
                let firstTime = 
                    (p.intervals 
                    |> Seq.toList 
                    |> List.head).tStart


                p.intervals
                |> Seq.iteri (fun i x ->
                    model.Annotations.Add(
                        Annotations.RectangleAnnotation( 
                            Layer = Annotations.AnnotationLayer.BelowSeries, 
                            MinimumX = x.tStart.x,
                            MaximumX = x.tEnd.x,
                            MinimumY = yBot,
                            MaximumY = yTop1,
                            Text = sprintf "%.1f" (x.tStart.x - firstTime.x),
                            Fill = intervalColor x.tType,
                            TextColor = if x.tType <> EyeEvent.Blink then OxyColors.Black else OxyColors.White
                        )
                    )
                )


            if (Seq.length p.blinkIntervals) > 0 then 
                let firstBlinkTime = 
                    (p.blinkIntervals 
                    |> Seq.toList 
                    |> List.head).tStart

                p.blinkIntervals
                |> Seq.iteri (fun i x ->
                    model.Annotations.Add(
                        Annotations.RectangleAnnotation( 
                            Layer = Annotations.AnnotationLayer.BelowSeries, 
                            MinimumX = x.tStart.x,
                            MaximumX = x.tEnd.x,
                            MinimumY = yBotBot,
                            MaximumY = yBot,
                            Text = sprintf "%.1f / %0.1f" (x.tStart.x - firstBlinkTime.x) (x.tEnd.x - x.tStart.x),
                            Fill = intervalColor x.tType,
                            TextColor = if x.tType <> EyeEvent.Blink then OxyColors.Black else OxyColors.White
                        )
                    )
                )


            let rec nrWithIn (timeRef:float) (ms:float) (idx) (count) =
                if idx = 0 || timeRef - (fst p.timeComments.[idx]) > ms then 
                    count
                else if idx = 0 then
                    count+1
                else     
                    nrWithIn timeRef ms (idx-1) (count+1)                   

            let nrOverlaps = 
                p.timeComments
                |> Array.mapi( fun i (tim, _) -> float (nrWithIn tim 30.0 i 0) )

            p.timeComments
            |> Seq.iteri (fun i (tim,txt) ->
                model.Annotations.Add(
                    Annotations.TextAnnotation(
                        Text = txt,
                        TextPosition = new DataPoint( tim, 
                            10.0 + nrOverlaps.[i] * 5.0 ),
                        TextHorizontalAlignment = HorizontalAlignment.Left
                        )
                    )
                )
           

        if p.config |> List.contains "NLRegression" then
            let saccTitles =
                p.saccsData
                |> Seq.map( fun s -> sprintf "A:%.2f B:%.2f C:%.2f D;%.2f" s.Abcd.A s.Abcd.B s.Abcd.C s.Abcd.D )
                |> Seq.toArray

            p.saccs
            |> Seq.iteri (fun i x ->
                model.Annotations.Add(
                    Annotations.RectangleAnnotation( 
                        Layer = Annotations.AnnotationLayer.BelowSeries, 
                        MinimumX = x.x,
                        MaximumX = x.y,
                        MinimumY = yTop1,
                        MaximumY = yTop2,
                        Text = sprintf "%d: %s" i saccTitles.[i],
                        Fill = OxyColor.FromRgb(255uy, 220uy, 255uy),
                        TextColor = OxyColors.Black
                    )
                )
            )

        // -- setup axis
        let aMin = min (Seq.head dataSpeed).X (Seq.last dataSpeed).X
        let aMax = max (Seq.head dataSpeed).X (Seq.last dataSpeed).X

        match p.xZoom with
        | Some x ->
            model.Axes.Add(
                Axes.LinearAxis(
                    Title = "TimeStamp [ms]",
                    Position = Axes.AxisPosition.Bottom,
                    AbsoluteMinimum = aMin,
                    AbsoluteMaximum = aMax,
                    Minimum = x.Start,
                    Maximum = x.End ) )
        | None -> 
            model.Axes.Add(
                Axes.LinearAxis(
                    Title = "TimeStamp [ms]",
                    Position = Axes.AxisPosition.Bottom,
                    AbsoluteMinimum = aMin,
                    AbsoluteMaximum = aMax ) )

        model.Axes.Add(
            Axes.LinearAxis(
                Title = "Speed [°/(20 micro-secs)], Error [°], Pupil Size [mm]",
                Position = Axes.AxisPosition.Left,
                AbsoluteMinimum = -200.0, //minDisplay,
                Minimum = -40.0, //minDisplay,
                AbsoluteMaximum = 200.0, //maxDisplay * 1.1,
                Maximum = 40.0 //maxDisplay * 1.1,
                //IsZoomEnabled = false
            )
        )

    model.InvalidatePlot( true )



let warp (xOld:float) mouseX (slopeX:float) (aMax:float) =
    let s = slopeX
    let m = mouseX 
    let a = aMax

    if xOld < mouseX then 
        let a0 = 0.0
        let a1 = 1.0
        let a2 = (s - 1.0) / (-m)
        let a3 = (s - 1.0) / (m**2.0)
        let x = xOld

        let p1 = a3*x*x*x + a2*x*x + a1*x + a0
        p1
    else

        let am = a - m

        let a0 = 0.0
        let a1 = (am + am*(2.0*s - 2.0) - am*(s - 1.0)) / am
        let a2 = (2.0*s - 2.0) / (-am)
        let a3 =  (s - 1.0) / (am**2.0)
        let x = xOld - m

        let p2 = a3*x*x*x + a2*x*x + a1*x + a0

        p2 + m


let updateAllTargets  
    (model:OxyPlot.PlotModel) 
    (vTargets: (int*Target)[])
    (updateToState: int -> unit) 
    =
    // printfn "updateAllTargets" 
    let selected = 
        match UiState.state with
        | None -> failwith "NoState"
        | Some x -> x.TargetNr

    let targetsIds = 
        vTargets
        |> Array.map( fun x -> (snd x).IDName)
        |> Array.distinct
        |> Array.sort

    let bgColor = OxyColors.White

    let axMax = (float) vTargets.Length
    //let axMax = 200.0
   
    let xAxis = 
       Axes.LinearAxis(
           Position = Axes.AxisPosition.Bottom,
           AbsoluteMinimum = 0.0,
           AbsoluteMaximum = axMax,
           Minimum = 0.0,
           Maximum = axMax,
           IsZoomEnabled = true,
           IsPanEnabled = true,
           IsAxisVisible = false)

    xAxis.Minimum <- 0.0
    xAxis.Maximum <- float (vTargets.Length-1)


    let yAxis = 
        Axes.LinearAxis(
            Position = Axes.AxisPosition.Left,
            AbsoluteMinimum = -1.0, 
            Minimum = -1.0, 
            AbsoluteMaximum = 1.0,
            Maximum = 1.0,
            IsAxisVisible = false
            )


    model.PlotAreaBorderThickness <- OxyThickness 0.0

    model.Annotations.Clear()

    let state = UiState.state.Value
    let mouseLocal = state.MousePosition.x / state.PlotWidth * axMax

    //vTargets
    [0 .. (vTargets.Length-1)]
    |> Seq.iteri (fun i x ->
        // TODO make warp factor configurable
        let ann = 
            Annotations.RectangleAnnotation( 
                Layer = Annotations.AnnotationLayer.BelowSeries, 
                MinimumX = (warp ((float) i) mouseLocal 3.0 axMax),
                MaximumX = (warp ((float) (i+1)) mouseLocal 3.0 axMax),
                MinimumY = -10.0,
                MaximumY = 10.0,
                Fill = 
                    match (Array.findIndex (fun xt -> xt = (snd vTargets.[i]).IDName) targetsIds) % targetsIds.Length with
                    | 0 -> OxyColors.LightYellow
                    | 1 -> OxyColors.LightCyan
                    | 2 -> OxyColors.LightGray
                    | 3 -> OxyColors.LightGreen
                    | 4 -> OxyColors.LightBlue
                    | _ -> OxyColors.LightSkyBlue

                // Text = sprintf "%d: %s" i saccTitles.[i],
            )
        
        if i = selected then
            ann.Fill <- OxyColors.Black

        ann.MouseDown.Add(fun ev -> updateToState (i))

        model.Annotations.Add( ann ) 
    )
    
    model.KeyDown.Add(fun ke ->
        // save to state vars and load from state L456
        // printfn "key pressed in target view %A" ke.Key 
        match ke.Key with
        | OxyKey.L -> xAxis.Minimum <- xAxis.Minimum + 1.0
        | OxyKey.K -> xAxis.Minimum <- xAxis.Minimum - 1.0
        | _ -> ()

        )

    model.Axes.Add( xAxis )
    model.Axes.Add( yAxis )

    model.Background <- bgColor
    model.InvalidatePlot(true)

(*
let updateBlinks  
    (model:OxyPlot.PlotModel) 
    =
    model.Background <- OxyColors.LightYellow
    model.InvalidatePlot(true)
*)

let updateBlinks 
    (model:OxyPlot.PlotModel)
    (data : BIntervalData[])
    (targetRange : Range<float> option) =
    
    model.Background <- OxyColors.White
    model.Series.Clear()
    model.Annotations.Clear()
    model.Axes.Clear()

    let cols = [| 
        OxyColors.LightSkyBlue
        OxyColors.LightSlateGray
        OxyColors.LightSteelBlue
        OxyColors.LightYellow
        |]

    if data.Length <> 0 then
        let scatterSeries = 
            new Series.ScatterSeries( 
                MarkerType = MarkerType.Circle,
                MarkerStroke = OxyColors.Black )

        let colorAxis = 
            new Axes.LinearColorAxis(
                Palette = OxyPalettes.Gray(32) )
        model.Axes.Add(colorAxis)

        let mutable minLen = 1e8
        let mutable maxLen = 0.0

        let startEnds =
            let mn = data.[0].time.Start
            let mx = data.[data.Length-1].time.End
            (mn, mx)

        let minX = fst startEnds
        let maxAllLen = (snd startEnds) - minX 

        data
        |> Seq.filter( fun x -> x.iType = EyeEvent.Blink )
        |> Seq.iter (fun b ->
            let len = b.time.End - b.time.Start
            minLen <- min minLen len
            maxLen <- max maxLen len )

        let startT = minX

        data
        |> Seq.filter( fun x -> x.iType = EyeEvent.Blink )
        |> Seq.iteri (fun j b -> 
            let len = b.time.End - b.time.Start
            let lenNorm = (len - minLen) / (maxLen-minLen)
            scatterSeries.Points.Add(
                Series.ScatterPoint(
                    0.5 * (b.time.Start + b.time.End) - startT, 
                    0.7 * lenNorm,
                    0.01 * len, 
                    len)) 
         )

        data
        |> Seq.iteri (fun j b ->
            model.Annotations.Add(
                Annotations.RectangleAnnotation( 
                    Layer = Annotations.AnnotationLayer.BelowSeries, 
                    MinimumX = b.time.Start - minX,
                    MaximumX = b.time.End - minX,
                    MinimumY = 0.0,
                    MaximumY = 1.0,
                    Fill = 
                        if b.iType <> EyeEvent.Blink then
                            cols.[(j/2)%cols.Length].ChangeSaturation(0.3)
                        else
                            OxyColors.Black
                    )
                )
            )

        model.Series.Add( scatterSeries )

        model.Axes.Add(
            Axes.LinearAxis(
                Title = "Time",
                Position = Axes.AxisPosition.Bottom,
                Minimum = 0.0,
                Maximum = maxAllLen,
                IsAxisVisible = false ) )

        model.Axes.Add(
            Axes.LinearAxis(
                Title = "BlinkLength",
                Position = Axes.AxisPosition.Left,
                Minimum = 0.0,
                Maximum = 1.0,
                IsAxisVisible = false) )

        match targetRange with
        | Some t -> 
            // printfn "active range: %A %A" t.Start t.End 
            let targetMarker = 
                Annotations.RectangleAnnotation(
                    Layer = Annotations.AnnotationLayer.BelowSeries, 
                    MinimumX = t.Start - minX,
                    MaximumX = t.End - minX,
                    MinimumY = 0.0,
                    MaximumY = 1.0,
                    Fill = OxyColors.LightSalmon,
                    StrokeThickness = 1.5,
                    Stroke = OxyColors.Red
                    )
            model.Annotations.Add( targetMarker )
        | None -> ()

        model.InvalidatePlot( true )
    else
        // model.PlotAreaBackground <- OxyColors.White 
        model.InvalidatePlot( true )
    ()
   