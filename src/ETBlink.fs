module ETBlink

open OxyPlot
open System
open System.IO
open System.Windows.Forms
open OxyPlot

// blinky includes
open ETData
open FSharp.Data
open System.Globalization
open OfficeOpenXml


let saveMetaBlinksTxt (filename) (targets:BTarget[]) =
    let invalidsLeft  = targets |> Array.filter( fun x -> x.PupilDia.Left = -1.0f ) |> Array.length
    let invalidsRight = targets |> Array.filter( fun x -> x.PupilDia.Right = -1.0f )|> Array.length

    let nrTarget = targets.Length

    let meanLeft = 
        targets 
        |> Array.filter( fun x -> x.PupilDia.Left <> -1.0f ) 
        |> Array.averageBy( fun x -> x.PupilDia.Left )

    let meanRight = 
        targets 
        |> Array.filter( fun x -> x.PupilDia.Right <> -1.0f ) 
        |> Array.averageBy( fun x -> x.PupilDia.Right )

    let diffSqLeft = 
        targets 
        |> Array.filter( fun x -> x.PupilDia.Left <> -1.0f ) 
        |> Array.map( fun x -> (meanLeft - x.PupilDia.Left) ** 2.0f )

    let stdLeft = (diffSqLeft |> Array.sum) / (float32 diffSqLeft.Length)

    let diffSqRight = 
        targets 
        |> Array.filter( fun x -> x.PupilDia.Right <> -1.0f ) 
        |> Array.map( fun x -> (meanRight - x.PupilDia.Right) ** 2.0f )

    let stdRight = (diffSqRight |> Array.sum) / (float32 diffSqRight.Length)

    (   filename,
        [|
            sprintf "NrTotalMeasurements = %d" nrTarget
            sprintf "NrInvalidsLeft = %d" invalidsLeft
            sprintf "NrInvalidsRight = %d" invalidsRight
            sprintf "NrInvalidsLeftPercent = %.1f" ((float invalidsLeft) / (float nrTarget) * 100.0)
            sprintf "NrInvalidsRightPercent = %.1f" ((float invalidsRight) / (float nrTarget) * 100.0)
            sprintf "MeanPupilDiameterLeft = %.3f" meanLeft
            sprintf "MeanPupilDiameterRight = %.3f" meanRight
            sprintf "StdPupilDiameterLeft = %.4f" stdLeft
            sprintf "StdPupilDiameterRight = %.4f" stdRight
        |] )
    |> File.WriteAllLines

    ()

let richFlatDataSeries 
    (hershBlinksL:BIntervalData[]) 
    (hershBlinksR:BIntervalData[])
    (hershBlinksA:BIntervalData[])
    (targets:BTarget[]) =

    let insideH (intervalHA:BIntervalData) (idx:int) =
        if idx >= intervalHA.idxRHersh.Start && idx <= intervalHA.idxRHersh.End then
            1
        else
            0

    let insideR (intervalHA:BIntervalData) (idx:int) =
        if idx >= intervalHA.idxRangeRaw.Start && idx <= intervalHA.idxRangeRaw.End then
            1
        else
            0

    targets
    |> Array.mapi( fun i x ->
        {
            HershRawLeft  = (hershBlinksL |> Array.map( fun b -> insideR b i ) |> Array.sum)   
            HershLeft     = (hershBlinksL |> Array.map( fun b -> insideH b i ) |> Array.sum)       
            HershRawRight = (hershBlinksR |> Array.map( fun b -> insideR b i ) |> Array.sum)
            HershRight    = (hershBlinksR |> Array.map( fun b -> insideH b i ) |> Array.sum)
            HershRawBoth  = (hershBlinksA |> Array.map( fun b -> insideR b i ) |> Array.sum)   
            HershBoth     = (hershBlinksA |> Array.map( fun b -> insideH b i ) |> Array.sum) 
        } )

(*
    let saveRawBlinks (filename) (blinks: (string * BIntervalData[])[]) (targets:BTarget[])=
        // --prepare file
        if File.Exists( filename ) then File.Delete( filename )
                
        let newFile = new FileInfo( filename )
        use pck = new ExcelPackage( newFile )
    
        blinks
        |> Seq.iter( fun (name, b) ->
            // --prepare file for blinks
            let wsB  = pck.Workbook.Worksheets.Add( name )
            wsB.Cells.["A1"].Value <- "BlinkNr"
            wsB.Cells.["B1"].Value <- "RawStart"
            wsB.Cells.["C1"].Value <- "HershAStart"
            wsB.Cells.["D1"].Value <- "HershLStart"
            wsB.Cells.["E1"].Value <- "HershRStart"

            wsB.Cells.["F1"].Value <- "RawStartIdx"
            wsB.Cells.["G1"].Value <- "HershAStartIdx"
            wsB.Cells.["H1"].Value <- "HershLStartIdx"
            wsB.Cells.["I1"].Value <- "HershRStartIdx"

            wsB.Cells.["J1"].Value <- "RawEnd"
            wsB.Cells.["K1"].Value <- "HershAEnd"
            wsB.Cells.["L1"].Value <- "HershLEnd"
            wsB.Cells.["M1"].Value <- "HershREnd"

            wsB.Cells.["N1"].Value <- "RawEndIdx"
            wsB.Cells.["O1"].Value <- "HershAEndIdx"
            wsB.Cells.["P1"].Value <- "HershLEndIdx"
            wsB.Cells.["Q1"].Value <- "HershREndIdx"

            wsB.Cells.["R1"].Value <- "Duration (ms)"
            wsB.Cells.["S1"].Value <- "Duration HershA (ms)"
            wsB.Cells.["T1"].Value <- "Inbetween (s)"
            wsB.Cells.["X1"].Value <- "Type"

    
            let blinksArr = b |> Seq.toArray
    
            wsB.Column(1).Style.Numberformat.Format <- "0"
            [2 .. 6]
            |> Seq.iter( fun i -> 
                wsB.Column(i).Style.Numberformat.Format <- "0.00" 
                wsB.Column(i).Width <- 15.0 )
    
            blinksArr
            |> Array.iteri( fun i x ->
                wsB.Cells.[ (sprintf "A%d" (i+2)) ].Value <- i+1
                wsB.Cells.[ (sprintf "B%d" (i+2)) ].Value <- x.tStart
                wsB.Cells.[ (sprintf "C%d" (i+2)) ].Value <- targets.[x.idxRHershA.Start].RecordingTimeStamp
                wsB.Cells.[ (sprintf "D%d" (i+2)) ].Value <- targets.[x.idxRHershL.Start].RecordingTimeStamp
                wsB.Cells.[ (sprintf "E%d" (i+2)) ].Value <- targets.[x.idxRHershR.Start].RecordingTimeStamp

                wsB.Cells.[ (sprintf "F%d" (i+2)) ].Value <- x.idxRangeRaw.Start
                wsB.Cells.[ (sprintf "G%d" (i+2)) ].Value <- x.idxRHershA.Start
                wsB.Cells.[ (sprintf "H%d" (i+2)) ].Value <- x.idxRHershL.Start
                wsB.Cells.[ (sprintf "I%d" (i+2)) ].Value <- x.idxRHershR.Start

                wsB.Cells.[ (sprintf "J%d" (i+2)) ].Value <- x.tEnd
                wsB.Cells.[ (sprintf "K%d" (i+2)) ].Value <- targets.[x.idxRHershA.End].RecordingTimeStamp
                wsB.Cells.[ (sprintf "L%d" (i+2)) ].Value <- targets.[x.idxRHershL.End].RecordingTimeStamp
                wsB.Cells.[ (sprintf "M%d" (i+2)) ].Value <- targets.[x.idxRHershR.End].RecordingTimeStamp

                wsB.Cells.[ (sprintf "N%d" (i+2)) ].Value <- x.idxRangeRaw.End
                wsB.Cells.[ (sprintf "O%d" (i+2)) ].Value <- x.idxRHershA.End
                wsB.Cells.[ (sprintf "P%d" (i+2)) ].Value <- x.idxRHershL.End
                wsB.Cells.[ (sprintf "Q%d" (i+2)) ].Value <- x.idxRHershR.End

                wsB.Cells.[ (sprintf "R%d" (i+2)) ].Value <- x.tEnd - x.tStart 
                wsB.Cells.[ (sprintf "S%d" (i+2)) ].Value <- targets.[x.idxRHershA.End].RecordingTimeStamp - targets.[x.idxRHershA.Start].RecordingTimeStamp
                wsB.Cells.[ (sprintf "T%d" (i+2)) ].Value <- 
                    if i > 0 then
                        (x.tStart - blinksArr.[i-1].tEnd) / 1000.0
                    else 
                        0.0   
                wsB.Cells.[ (sprintf "X%d" (i+2)) ].Value   <- 
                    match x.tType with
                    | BEyeEvent.Blink  -> "Blink"
                    | BEyeEvent.Unknown s -> s
                    | BadData -> "BadData"
                    | _ -> "Unknown"
                        )
            )
        pck.Save()
    
        ()
*)


let movingAvg (span) (data:float32[]) (skipVal) (idx) =
    let s = -span/2
    let e = span/2

    let mutable count = 0
    let mutable weightSum = 0

    let localMinTmp = 
        [| s .. e |]
        |> Array.map( fun elem ->
            let i = idx + elem
            if i > 0 && i < data.Length then
                data.[i] 
            else
                0.0f )
        |> Array.filter( fun x -> x <> skipVal )

    let localMin = if localMinTmp.Length > 0 then  Array.min localMinTmp else -1.0f

    let weights = 
        [| s .. e |]
        |> Array.map( fun x -> 
            if x < 1 then 
                span/2 + x + 1
            else
                - x + span/2 + 1)

    let sum = 
        (0.0f, [| s .. e |])
        ||> Array.fold( fun acc elem ->
            let i = idx + elem
            if i > 0 && i < data.Length then
                let w = weights.[s + span/2]
                weightSum <- weightSum + w
                count <- count + 1
                if data.[i] <> skipVal then
                    acc + data.[i] * (float32 w)
                else
                    acc + 0.0f

            (* if i > 0 && i < data.Length then // && data.[i] <> skipVal then
                count <- count + 1
                if data.[i] <> skipVal then
                    acc + data.[i]
                else
                    acc + localMin *)
            else
                acc + 0.0f
            )

    if count <> 0 then
        //sum / (float32 count)
        sum / (float32 weightSum)
    else
        0.0f // -1.0f

let diff (a:float32[]) (t:float[]) =
    [| 0 .. a.Length-1 |]
    |> Array.map( fun i -> 
        if i = 0 then
            0.0f
        else
            float32( float (a.[i]-a.[i-1]) / (t.[i] - t.[i-1]) ) )
   
let rec corrOnSet (onSet:int) (diffSmoothPupil:float32[]) = 
    if onSet <= 0 then
        0
    elif diffSmoothPupil.[onSet] <= 0.0f then
        corrOnSet (onSet-1) diffSmoothPupil
    else
        onSet

let rec corrOffSet (offSet:int) (diffSmoothPupil:float32[]) = 
    if offSet >= diffSmoothPupil.Length-1 then
        diffSmoothPupil.Length-1
    elif diffSmoothPupil.[offSet] >= 0.0f then
        corrOffSet (offSet+1) diffSmoothPupil
    else
        offSet

let correctViaHershmann (dataTimeStamps:float[]) (dataPupilDias:float32[]) (samplingRateInHz) (intervals:BIntervalData[]) =
    let samlingInterval = Math.Round( 1000.0 / samplingRateInHz )
    let gapInterval = 100.0
    let msForSmoothing = 10.0

    let tmp = int (Math.Ceiling (msForSmoothing/samlingInterval))
    let span = if tmp % 2 <> 1 then tmp + 1 else tmp

//        let pupL = sampleData |> Array.map (fun x -> x.PupilDia.Left)
//        let pupR = sampleData |> Array.map (fun x -> x.PupilDia.Right)
//        let pupA = sampleData |> Array.map (fun x -> x.PupilDia.Avg)

//        let timestamps = sampleData |> Array.map (fun x -> x.RecordingTimeStamp)

//        let smoothPupils = pupL |> Array.mapi (fun i _ -> movingAvg span pupL -1.0f i)

    let smoothPup = dataPupilDias |> Array.mapi (fun i _ -> movingAvg span dataPupilDias -1.0f i)
//        let smoothPA = pupA |> Array.mapi (fun i _ -> movingAvg span pupA -1.0f i)

    let diffSmoothPup = diff dataPupilDias dataTimeStamps

//        let diffSmoothPR = diff smoothPR timestamps
//        let diffSmoothPA = diff smoothPA timestamps

    let intervalsHershed = 
        intervals 
        |> Array.mapi( fun i x -> 
            let corrPupS = corrOnSet (x.idxRangeRaw.Start+1) diffSmoothPup
            let corrPupE = corrOffSet (x.idxRangeRaw.End+1) diffSmoothPup
            { x with 
                idxRHersh = { Start = corrPupS; End = corrPupE } }
            )
(*
            let corrPRS = corrOnSet (x.idxRangeRaw.Start+1) diffSmoothPR
            let corrPRE = corrOffSet (x.idxRangeRaw.End+1) diffSmoothPR

            let corrPAS = corrOnSet (x.idxRangeRaw.Start+1) diffSmoothPA
            let corrPAE = corrOffSet (x.idxRangeRaw.End+1) diffSmoothPA
*)
 
(*
    let smoothedPupils = 
        smoothPL
        |> Array.mapi( fun i x -> 
            { Left = x
                Right = smoothPR.[i]
                Avg = smoothPA.[i]} )

    let diffPupils =
        diffSmoothPL
        |> Array.mapi( fun i x -> 
            { Left = x
                Right = diffSmoothPR.[i]
                Avg = diffSmoothPA.[i]} )
*)

    (intervalsHershed, smoothPup, diffSmoothPup )

     
let showBlinkPlots (data:BTarget[]) (intervals:BIntervalData[]) (smoothed:PupilDiameter[]) (diffed:PupilDiameter[]) =
        
    // -- prepare dialog and plot view
    let model  = new PlotModel()

    use plot =
        new WindowsForms.PlotView( 
            Size = System.Drawing.Size( 1024, 512 ), 
            Dock = DockStyle.Fill )


    use win =
        new Form(
            Text = "Blink View",
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable,
            ClientSize = plot.Size + Drawing.Size( 0, 30 ),
            ShowIcon = false
        )

       
    win.Controls.Add( plot )

    win.ShowDialog() |> ignore  

    let (<**) (points) (series:Series.DataPointSeries)  =
        let points = Array.ofSeq points
        series.Points.AddRange(points)
        model.Series.Add(series)
        points

    let (<++) (points) (series:Series.ScatterSeries)  =
        let points = Array.ofSeq points
        series.Points.AddRange(points)
        model.Series.Add(series)
        points

    intervals
    |> Array.iteri( fun i x ->
            
        model.Series.Clear() 
        model.Axes.Clear()
        model.Annotations.Clear()

        let minIdx = 
            [ x.idxRangeRaw.Start; x.idxRHersh.Start; x.idxRHersh.Start; x.idxRHersh.Start ] 
            |> Seq.min
            |> (+) (-10)
            |> max 0

        let maxIdx = 
            [ x.idxRangeRaw.End; x.idxRHersh.End; x.idxRHersh.End; x.idxRHersh.End] 
            |> Seq.max
            |> (+) 10
            |> min (data.Length-1)

        let dataPupilLeft =
            [| minIdx .. maxIdx |]
            //|> Array.filter (fun i -> data.[i].PupilDia.Avg <> -1.0f)
            |> Array.map (fun i ->
                DataPoint( data.[i].RecordingTimeStamp, float (data.[i].PupilDia.Left) ) )

            <** Series.LineSeries( Title = "RPLeft", Color = OxyColors.Black)

        let dataPupilRight =
            [| minIdx .. maxIdx |]
            //|> Array.filter (fun i -> data.[i].PupilDia.Avg <> -1.0f)
            |> Array.map (fun i ->
                DataPoint( data.[i].RecordingTimeStamp, float (data.[i].PupilDia.Right) ) )

            <** Series.LineSeries( Title = "RPRight", Color = OxyColors.Gray)
                  
        let smoothDataPupilLeft =
            [| minIdx .. maxIdx |]
            //|> Array.filter (fun i -> data.[i].PupilDia.Avg <> -1.0f)
            |> Array.map( fun i ->
                DataPoint( data.[i].RecordingTimeStamp, float (smoothed.[i].Left) ) )

            <** Series.LineSeries( Title = "SRPLeft", Color = OxyColors.LightBlue)

        let diffDataPupilLeft =
            [| minIdx .. maxIdx |]
            //|> Array.filter (fun i -> data.[i].PupilDia.Avg <> -1.0f)
            |> Array.map( fun i -> 
                DataPoint( data.[i].RecordingTimeStamp, float (diffed.[i].Left) ) )

            <** Series.LineSeries( Title = "DRPLeft", Color = OxyColors.Red)
          

        model.Annotations.Add(
            Annotations.RectangleAnnotation( 
                Layer = Annotations.AnnotationLayer.BelowSeries, 
                MinimumX = data.[x.idxRangeRaw.Start].RecordingTimeStamp,
                MaximumX = data.[x.idxRangeRaw.End].RecordingTimeStamp,
                MinimumY = 1.75,
                MaximumY = 2.0,
                Text = sprintf "Raw %d: %d, Len: %.0f" 
                    x.idxRangeRaw.Start 
                    x.idxRangeRaw.End
                    (data.[x.idxRangeRaw.End].RecordingTimeStamp - data.[x.idxRangeRaw.Start].RecordingTimeStamp),
                Fill = OxyColor.FromRgb(200uy, 200uy, 200uy),
                TextColor = OxyColors.Black
            ) )

        model.Annotations.Add(
            Annotations.RectangleAnnotation( 
                Layer = Annotations.AnnotationLayer.BelowSeries, 
                MinimumX = data.[x.idxRHersh.Start].RecordingTimeStamp,
                MaximumX = data.[x.idxRHersh.End].RecordingTimeStamp,
                MinimumY = 1.5,
                MaximumY = 1.75,
                Text = sprintf "Hersh %d: %d, Len: %.0f" 
                    x.idxRHersh.Start 
                    x.idxRHersh.End 
                    (data.[x.idxRHersh.End].RecordingTimeStamp - data.[x.idxRHersh.Start].RecordingTimeStamp),
                Fill = OxyColor.FromRgb(220uy, 255uy, 220uy),
                TextColor = OxyColors.DarkGreen
            ) )

        model.Background <- OxyColors.White

        model.Axes.Add(
            Axes.LinearAxis(
                Title = "TimeStamp [ms]",
                Position = Axes.AxisPosition.Bottom,
                AbsoluteMinimum = data.[minIdx].RecordingTimeStamp,
                AbsoluteMaximum = data.[maxIdx].RecordingTimeStamp,
                Minimum = data.[minIdx].RecordingTimeStamp,
                Maximum = data.[maxIdx].RecordingTimeStamp ) )

        model.Axes.Add(
            Axes.LinearAxis(
                Title = "Dia",
                Position = Axes.AxisPosition.Left,
                AbsoluteMinimum = -2.0, //minDisplay,
                Minimum = -2.0, //minDisplay,
                AbsoluteMaximum = 6.0, //maxDisplay * 1.1,
                Maximum = 6.0 //maxDisplay * 1.1,
                //IsZoomEnabled = false
            ) )

            

        model.InvalidatePlot( true )
        plot.Model <- model

        printfn "plotting %d-%d: %f-%f" minIdx maxIdx data.[minIdx].RecordingTimeStamp data.[maxIdx].RecordingTimeStamp
        win.ShowDialog() |> ignore
    )
       
    ()

let BIntervalDataAsTsv (filePath) (bIntervals:BIntervalData[]) (inbetweenArr:float[])=
    let mutable tsvLines = List.Empty
    tsvLines <- tsvLines @ [ BIntervalData.TsvHeader() ]

    bIntervals 
    |> Array.iteri( fun i x -> 
        printfn "%d: %s, inbetween: %f" i (x.toStr()) (inbetweenArr.[i])
        tsvLines <- tsvLines @ [ x.toTsvStr(inbetweenArr.[i]) ]
        )

    IO.File.WriteAllLines(filePath, tsvLines)

let getValidIntervalsAndInbetweens (intervals) =
    let validIntArr = 
        intervals
        |> Seq.filter( fun x -> x.iType = EyeEvent.Blink)
        |> Seq.toArray

    let inbetweenArr =
        validIntArr
        |> Array.mapi( fun i x -> 
            if i > 0 then
                x.time.Start - validIntArr.[i-1].time.End
            else
                0.0
                )
    (validIntArr, inbetweenArr)

let hershIntervals (dataTimeStamps:float[]) (dataPupils:float32[]) (intervals : BIntervalData[]) =
    let validIntArr = 
        intervals
        |> Seq.filter( fun x -> x.iType = EyeEvent.Blink)
        |> Seq.toArray

    let inbetweenArr =
        validIntArr
        |> Array.mapi( fun i x -> 
            if i > 0 then
                x.time.Start - validIntArr.[i-1].time.End
            else
                0.0
                )

    let chopOffLastElementList (list: List<_>) =
        if list.Length >= 2 then
            list.[..(list.Length - 2)]
        else // list.Length = 1 then
            List.Empty

    let gapInbetween = 60.0
    
    let mutable mergedValids = List.Empty
    validIntArr 
    |> Array.iteri( fun i x -> 
        if inbetweenArr.[i] < gapInbetween && i > 0 then
            mergedValids <- chopOffLastElementList mergedValids
            mergedValids <- mergedValids @ [
            { BIntervalData.Zero with 
                    time= {Start= validIntArr.[i-1].time.Start; End= x.time.End }
                    iType= Unknown "merged" 
                    idxRangeRaw=x.idxRangeRaw }] 
        else 
            mergedValids <- mergedValids @ [x]
        )
   
  
    validIntArr 
    |> Array.iteri( fun i x -> 
        printfn "%d: %s, inbetween: %f" i (x.toStr()) (inbetweenArr.[i])
        )




    
    let lerp (x0:float32) (y0:PupilDiameter) x1 (y1:PupilDiameter) x =
        { 
            Left = y0.Left + (x - x0) * (y1.Left-y0.Left) / (x1-x0)
            Right = y0.Right + (x - x0) * (y1.Right-y0.Right) / (x1-x0)
            Avg = y0.Avg+ (x - x0) * (y1.Avg-y0.Avg) / (x1-x0)
        }

    let linFill (dat:BTarget[]) (interv) =
        interv
        |> Seq.iter( fun x -> 
            [| x.idxRangeRaw.Start .. x.idxRangeRaw.End-1 |]
            |> Array.iter( fun i -> 
                dat.[i] <- 
                    { dat.[i] with 
                        PupilDia = 
                            lerp 
                                (float32 x.time.Start) 
                                dat.[x.idxRangeRaw.Start].PupilDia
                                (float32 x.time.End)
                                dat.[x.idxRangeRaw.End-1].PupilDia
                                (float32 dat.[i].RecordingTimeStamp)
                    } ) )
        dat       

    //let dat = linFill data intervals

    let correctedIntArr, smoothed, diffed = 
        validIntArr 
        |> correctViaHershmann dataTimeStamps dataPupils 50.0  

    (correctedIntArr, inbetweenArr, smoothed, diffed)


    //                         time*validity 
let getIntervals (errArr:(float*bool)[]) =

    let lastIdx = errArr.Length-1
     
    let eArr = (errArr |> Array.unzip |> fst) // time stamps
    let vArr = (errArr |> Array.unzip |> snd) // validity values

    let mutable intervals = List.Empty
    let mutable startIdx = 1

    for i in 0 .. lastIdx do
        if i > 0 then
            if vArr.[i-1] = false && vArr.[i] then // split on offset
                let ms = eArr.[i-1] - eArr.[startIdx]
                // let msRange = BBlink.Limits()
                intervals <- intervals @ 
                    [{ BIntervalData.Zero with 
                        time= { Start= eArr.[startIdx]; End= eArr.[i-1] }
                        iType= if BlinkLimits.inside ms then EyeEvent.Blink else EyeEvent.BadData
                            // if ms < msRange.x || ms > msRange.y then BEyeEvent.BadData else BEyeEvent.Blink
                        idxRangeRaw = { Start = startIdx; End = i - 1} }] 
                startIdx  <- i

            else if vArr.[i-1] && (vArr.[i] = false) then // split on onset
                intervals <- intervals @ 
                    [{ BIntervalData.Zero with 
                            time= { Start= eArr.[startIdx]; End= eArr.[i-1] }
                            iType= Unknown "good" 
                            idxRangeRaw = { Start = startIdx; End = i - 1} }] 
                startIdx  <- i

    
    // printfn "Intervals: %A" intervals

    intervals
(*
    let invIntervals (intervals: BIntervalData list) =
        
        let intArr = 
            intervals
            |> Seq.toArray

        [0 .. intArr.Length-1]
        |> Array.mapi( i ->  )
*)

let processForBlinks (blinkFile) =
    printfn "blinkFile: %s" blinkFile
    
    let cleanedFile = Csv.cleanHashCommentsFromASCIIFile (blinkFile)
    let tsvFile = CsvFile.Load( cleanedFile ) 
            
    printfn "Rows: %d" (Seq.length tsvFile.Rows)

    printfn "Headers: %A" tsvFile.Headers

    let headers = tsvFile.Headers.Value

    let data = 
        tsvFile.Rows
        |> Seq.mapi( fun i x ->

            let readAndReplace (s:string) =
                let s1 = (x.GetColumn s)
                s1.Replace( ',', '.' )

            let readFloat (s) =
                try
                    Some (System.Single.Parse( readAndReplace s , CultureInfo.InvariantCulture ))
                with 
                | e -> 
                    //printfn "%A Warning: %A, col: %A str: %A" i e.Message s (x.GetColumn s)
                    None

            let readUInt64 (s:string) = 
                try
                    System.UInt64.Parse (x.GetColumn s)
                with 
                | e -> 
                    //printfn "%A Warning: %A, col: %A str: %A" i e.Message s (x.GetColumn s)
                    0UL

//                    printf "i: %d: " i

            let recording = float (readUInt64 "Recording timestamp")
            let timeStamp = float (readUInt64 "Computer timestamp")
            let pupilLeft = readFloat "Pupil diameter left"
            let pupilRight = readFloat "Pupil diameter right"
                    
            let valid  = (x.GetColumn "Validity left") = "Valid" && (x.GetColumn "Validity right") = "Valid"
            let validL = (x.GetColumn "Validity left") = "Valid"
            let validR = (x.GetColumn "Validity right") = "Valid"

            // let timeStamp = 0.0

//                    printfn "Rec, Time: %A, %A" recording timeStamp

            {
                RecordingTimeStamp = recording
                TimeStamp = timeStamp
                ValidBoth = if pupilLeft.IsSome && pupilRight.IsSome then valid else false
                ValidLeft = if pupilLeft.IsSome then validL else false
                ValidRight = if pupilRight.IsSome then validR else false
                PupilDia ={
                    Left = if pupilLeft.IsSome then pupilLeft.Value else -1.0f
                    Right = if pupilRight.IsSome then pupilRight.Value else -1.0f
                    Avg = 
                        if pupilLeft.IsSome && pupilRight.IsSome then 
                            0.5f * (pupilLeft.Value + pupilRight.Value)
                        else
                            -1.0f }
            }

            )
        |> Seq.toArray

    let intervalsLeft =
        data
        |> Seq.map( fun x -> (x.RecordingTimeStamp, x.ValidLeft) )
        |> Seq.toArray
        |> getIntervals
        |> Seq.toArray

    let intervalsRight =
        data
        |> Seq.map( fun x -> (x.RecordingTimeStamp, x.ValidRight) )
        |> Seq.toArray
        |> getIntervals
        |> Seq.toArray

    let intervalsAvg =
        data
        |> Seq.map( fun x -> (x.RecordingTimeStamp, x.ValidBoth) )
        |> Seq.toArray
        |> getIntervals
        |> Seq.toArray

    let intArrRawLeft, inbetweenRawLeft = getValidIntervalsAndInbetweens intervalsLeft
    let intArrRawRight, inbetweenRawRight = getValidIntervalsAndInbetweens intervalsRight
    let intArrRawAvg, inbetweenRawAvg = getValidIntervalsAndInbetweens intervalsAvg

    let allRecTimeStamps =  data |> Array.map( fun x -> x.RecordingTimeStamp)

    let allPupilsLeft  =  data |> Array.map( fun x -> x.PupilDia.Left )
    let allPupilsRight =  data |> Array.map( fun x -> x.PupilDia.Right )
    let allPupilsAvg   =  data |> Array.map( fun x -> x.PupilDia.Avg )

    let hershyLeft, inbetweenHLeft, _, _  = hershIntervals allRecTimeStamps allPupilsLeft intervalsLeft
    let hershyRight,inbetweenHRight, _, _ = hershIntervals allRecTimeStamps allPupilsRight intervalsRight
    let hershyAvg, inbetweenHAvg, _, _  = hershIntervals allRecTimeStamps allPupilsAvg intervalsAvg

    let filePathTsvHLeft = IO.Path.GetFullPath(blinkFile).Split('.').[0] + "_Blinks_HershLeft.tsv" 
    BIntervalDataAsTsv filePathTsvHLeft hershyLeft inbetweenHLeft
    let filePathTsvHRight = IO.Path.GetFullPath(blinkFile).Split('.').[0] + "_Blinks_HershRight.tsv" 
    BIntervalDataAsTsv filePathTsvHRight hershyRight inbetweenHRight
    let filePathTsvHAvg = IO.Path.GetFullPath(blinkFile).Split('.').[0] + "_Blinks_HershBoth.tsv" 
    BIntervalDataAsTsv filePathTsvHAvg hershyAvg inbetweenHAvg

    let filePathTsvRLeft = IO.Path.GetFullPath(blinkFile).Split('.').[0] + "_Blinks_RawLeft.tsv" 
    BIntervalDataAsTsv filePathTsvRLeft intArrRawLeft inbetweenRawLeft
    let filePathTsvRRight = IO.Path.GetFullPath(blinkFile).Split('.').[0] + "_Blinks_RawRight.tsv" 
    BIntervalDataAsTsv filePathTsvRRight intArrRawRight inbetweenRawRight
    let filePathTsvRAvg = IO.Path.GetFullPath(blinkFile).Split('.').[0] + "_Blinks_RawBoth.tsv" 
    BIntervalDataAsTsv filePathTsvRAvg intArrRawAvg inbetweenRawAvg

(*
    let excelName = blinkFile.Substring(0, blinkFile.Length-3) + "xlsx"
    printfn "saving: %A" excelName

    saveRawBlinks excelName [|
        ("AllValidBlinks", correctedIntArr )
        ("AllValidMergedBlinks", mergedValids |> Seq.toArray)
        ("AllIntervals", intervals |> Seq.toArray)
        |] data
        
    showBlinkPlots data correctedIntArr smoothed diffed
*)

    let richFlatHersh = richFlatDataSeries hershyLeft hershyRight hershyAvg data

    let tsvName = blinkFile.Substring(0, blinkFile.Length-4) + "_out.tsv"

    IO.saveValidBlinksTsv tsvName richFlatHersh data

    let metaName = blinkFile.Substring(0, blinkFile.Length-4) + "_meta.txt"
    saveMetaBlinksTxt metaName data

    printfn "saved: %A" tsvName
    ()
        
(*
showOpenFileDialog "Save Session" "" [
    "Tobii Eyetracker Data File", "tsv", fun filename -> openSession filename
    "Comma Seperated Data File", "csv", fun filename -> openSession filename
]
*)

let allBlinkHack(files : string[]) =
    let blinkFiles = 
        if files.Length = 0 then
            IO.showOpenMultiFileDialog "Select BlinkFile" "" [
                "Mobile Tobii Eyetracker Data File", "tsv", fun filename -> filename
                "Comma Seperated Data File", "csv", fun filename -> filename
                ]
            |> Option.map snd
        else
            let checkedFiles =
                files
                |> Array.filter(File.Exists)
            if checkedFiles.Length > 0 then
                Some checkedFiles
            else
                None

    if blinkFiles.IsSome then
        blinkFiles.Value
        |> Array.iter( processForBlinks )

// [x] columns for L R A
// [x] blink nur wenn beide invalid
// [x] invalid count stats in zusaetzliches file (excel tab)
// [] raw interval anpassen
// [] merge short blinks before delete (within 200ms)


let plotBlinkAnnotations
    (model:OxyPlot.PlotModel)
    (data : (BIntervalData list * string)[]) =
        
    let cols = [| 
(*            OxyColors.LightGreen
        OxyColors.LightBlue
        OxyColors.LightCoral
        OxyColors.LightCyan
        OxyColors.LightGoldenrodYellow
        OxyColors.LightGray
        OxyColors.LightPink
        OxyColors.LightSeaGreen
        OxyColors.LightSalmon *)
        OxyColors.LightSkyBlue
        OxyColors.LightSlateGray
        OxyColors.LightSteelBlue
        OxyColors.LightYellow
        |]

    model.Series.Clear() 

    let scatterSeries = 
        new Series.ScatterSeries( 
            MarkerType = MarkerType.Circle,
            MarkerStroke = OxyColors.Black )

    let colorAxis = 
        new Axes.LinearColorAxis(
            Palette = OxyPalettes.Gray(32) )

    let mutable minLen = 1e8
    let mutable maxLen = 0.0

    let startEnds =
        data 
        |> Seq.map( fun blinksPerPerson ->
            let blinks =
                blinksPerPerson
                |> fst
                |> Seq.toArray

            let mn = blinks.[0].time.Start
            let mx = blinks.[blinks.Length-1].time.End
            (mn, mx)
            )
        |> Seq.toArray

    let minX = startEnds |> Array.minBy( fun x -> fst x ) |> fst
    let maxAllLen =
            startEnds 
            |> Array.map( fun x -> (snd x) - (fst x))
            |> Array.max


    data
    |> Seq.iter (fun blinksPerPerson ->
        blinksPerPerson
        |> fst
        |> Seq.filter( fun x -> x.iType = EyeEvent.Blink )
        |> Seq.iter (fun b ->
            let len = b.time.End - b.time.Start
            minLen <- min minLen len
            maxLen <- max maxLen len)
        )

    model.Axes.Add(colorAxis)

    data
    |> Seq.iteri (fun  i blinksPerPerson ->
        let startT = 
            (blinksPerPerson
            |> fst
            |> List.head).time.Start

        blinksPerPerson
        |> fst
        |> Seq.filter( fun x -> x.iType = EyeEvent.Blink )
        |> Seq.iteri (fun j b -> 
            let len = b.time.End - b.time.Start
            let lenNorm = (len - minLen) / (maxLen-minLen)
            scatterSeries.Points.Add(
                Series.ScatterPoint(
                    0.5 * (b.time.Start + b.time.End) - startT, 
                    //(if (i % 2 = 0) then (float i) + 0.2 else (float i) + 0.8), 
                    //(if (i % 2 = 0) then (float i) + 0.6 else (float i) + 0.4),
                    //(if (i % 2 = 0) then (float i) + 0.2 + 0.8 * lenNorm else (float i) + 0.8 - 0.8 * lenNorm),
                    (if (i % 2 = 0) then (float i) + 0.3 + 0.7 * lenNorm else (float i) +  0.7 * lenNorm),
                    0.01 * len, 
                    len)))
            )

    // model.Axes.Clear()
    model.Annotations.Clear()

    data
    |> Seq.iteri (fun i blinksPerPerson ->
        let blinkList = blinksPerPerson |> fst
        let fileName =  blinksPerPerson |> snd

        let minY = (if (i % 2 = 0) then (float i) + 0.25 else (float i) )
        let maxY = (if (i % 2 = 0) then (float i) + 1.0 else (float i) + 0.75)
            
        model.Annotations.Add(
            Annotations.RectangleAnnotation( 
                Layer = Annotations.AnnotationLayer.BelowSeries, 
                MinimumX = maxAllLen,
                MaximumX = maxAllLen + 30000.0,
                MinimumY = minY,
                MaximumY = maxY,
                Fill = OxyColors.LightGreen,
                Text = fileName.Substring(fileName.Length/2)
        ) )

        let startT = 
            (blinksPerPerson
            |> fst
            |> List.head).time.Start

        blinksPerPerson
        |> fst
        |> Seq.iteri (fun j b ->

            model.Annotations.Add(
                Annotations.RectangleAnnotation( 
                    Layer = Annotations.AnnotationLayer.BelowSeries, 
                    MinimumX = b.time.Start - startT,
                    MaximumX = b.time.End - startT,
                    MinimumY = minY,
                    MaximumY = maxY,
                    Fill = 
                        if b.iType <> EyeEvent.Blink then
                            cols.[(j/2)%cols.Length]
                                .ChangeIntensity( (if (i % 2 = 0) then 0.85 else 1.0 )  )
                                .ChangeSaturation(0.3)
                        else
                            OxyColors.Black
                    // Text = sprintf "%.1f" (x.tStart.x - firstTime.x),
                    //TextColor = if x.tType <> EyeEvent.Blink then OxyColors.Black else OxyColors.White
                    )
                )
            )
        )

    model.Series.Add( scatterSeries )

    model.Axes.Add(
        Axes.LinearAxis(
            Title = "Time",
            Position = Axes.AxisPosition.Bottom,
            Minimum = 0.0,
            Maximum = maxAllLen  ) )

    model.Axes.Add(
        Axes.LinearAxis(
            Title = "Run",
            Position = Axes.AxisPosition.Left,
            Minimum = 0.0,
            Maximum = (float data.Length) + 1.0 ) )

    model.PlotAreaBackground <- OxyColors.White 
    model.InvalidatePlot( true )



let blinksToPlotData (files:string[]) =

    let plotData = 
        files
        |> Array.sort
        |> Array.map( fun fi -> 

            let tsvFile = CsvFile.Load( fi )
            let data = 
                tsvFile.Rows
                |> Seq.mapi( fun i x ->

                    let readAndReplace (s:string) =
                        let s1 = (x.GetColumn s)
                        s1.Replace( ',', '.' )

                    let readFloat (s) =
                        try
                            Some (System.Single.Parse( readAndReplace s , CultureInfo.InvariantCulture ))
                        with 
                        | e -> 
                            //printfn "%A Warning: %A, col: %A str: %A" i e.Message s (x.GetColumn s)
                            None

                    let readUInt64 (s:string) = 
                        try
                            System.UInt64.Parse (x.GetColumn s)
                        with 
                        | e -> 
                            //printfn "%A Warning: %A, col: %A str: %A" i e.Message s (x.GetColumn s)
                            0UL

    //                    printf "i: %d: " i

                    let recording = float (readUInt64 "Computer timestamp")
                    let blinkTmp = (readUInt64 "Raw Blink both") 
                    let blink = if blinkTmp = 1UL then false else true

                    (recording, blink)
                    )

            (data
            |> Seq.toArray
            |> getIntervals, fi)
            )
        
    //let plotDataInv = invIntervals plotData

    //(plotData, plotDataInv)
    plotData  

let selectAllBlinkFiles(files : string[]) =
    let blinkFiles = 
        if files.Length = 0 then
            IO.showOpenMultiFileDialog "Select BlinkFile" "" [
                ("Mobile Tobii Eyetracker Data File", "tsv", fun filename ->
                    filename
                ) ]
            |> Option.map snd
        else
            let checkedFiles =
                files
                |> Array.filter(File.Exists)
            if checkedFiles.Length > 0 then
                Some checkedFiles
            else
                None
    blinkFiles


// modification to Hershmann
// finite difference (mathematically more senseful)

let cleanResultFiles (resFile) =

    printfn "resFile: %s" resFile

    let tsvFile = CsvFile.Load( resFile) 
        
    printfn "Rows: %d" (Seq.length tsvFile.Rows)
    printfn "Headers: %A" tsvFile.Headers

    let headers = tsvFile.Headers.Value

    let data = 
        tsvFile.Rows
        |> Seq.mapi( fun i x ->

            let readAndReplace (s:string) =
                let s1 = (x.GetColumn s)
                s1.Replace( ',', '.' )

            let readFloat (s) =
                try
                    System.Single.Parse( readAndReplace s , CultureInfo.InvariantCulture )
                with 
                | e -> 
                    printfn "%A Warning: %A, col: %A str: %A" i e.Message s (x.GetColumn s)
                    0.0f

            let readUInt64 (s:string) = 
                try
                    System.UInt64.Parse (x.GetColumn s)
                with 
                | e -> 
                    printfn "%A Warning: %A, col: %A str: %A" i e.Message s (x.GetColumn s)
                    0UL

//                    printf "i: %d: " i

// Recording timestamp	Computer timestamp	Pupil diameter left	Pupil diameter right	Pupil diameter avg	Validity left	Validity right	Validity both	Raw Blink left	HershBlink left	Raw Blink right	HershBlink right	Raw Blink both	HershBlink avg

            let recording = readUInt64 "Recording timestamp"
            let timeStamp = readUInt64 "Computer timestamp"
            let pupilLeft = readFloat "Pupil diameter left"
            let pupilRight = readFloat "Pupil diameter right"
            let pupilAvg = readFloat "Pupil diameter avg"
                
            let valid  = x.GetColumn "Validity both"
            let validL = x.GetColumn "Validity left"
            let validR = x.GetColumn "Validity right"

            let blinkRawLeft = readUInt64 "Raw Blink left"
            let blinkRawRight = readUInt64 "Raw Blink right"
            let blinkRawBoth= readUInt64 "Raw Blink both" 
                
            sprintf "%d\t%d\t%.3f\t%.3f\t%.3f\t%s\t%s\t%s\t%d\t%d\t%d"
                recording
                timeStamp
                pupilLeft
                pupilRight
                pupilAvg
                valid
                validL
                validR
                blinkRawLeft
                blinkRawRight
                blinkRawBoth
            )
        |> Seq.toArray

    let fileNameExt = Path.GetFileName(resFile)
    let fullPath =  Path.GetFullPath(resFile)

    let fileName = 
        Path.Combine( 
            fullPath.Substring(0, fullPath.Length - fileNameExt.Length), 
            Path.GetFileNameWithoutExtension(resFile) + "_C" + 
            Path.GetExtension(resFile) ) 
        
    if File.Exists( fileName ) then File.Delete( fileName )

    let linesWithHeader = Array.concat [| 
        [|"Recording timestamp\tComputer timestamp\tPupil diameter left\tPupil diameter right\tPupil diameter avg\tValidity left\tValidity right\tValidity both\tRaw Blink left\tRaw Blink right\tRaw Blink both" |]
        data |]

    File.WriteAllLines(fileName, linesWithHeader)
    ()


let splitResultFiles (splits) (resFile)  =

    printfn "resFile: %s" resFile

    let tsvFile = CsvFile.Load( resFile) 
    
    printfn "Rows: %d" (Seq.length tsvFile.Rows)
    printfn "Headers: %A" tsvFile.Headers

    let headers = tsvFile.Headers.Value

    let data = 
        tsvFile.Rows
        |> Seq.mapi( fun i x ->

            let readAndReplace (s:string) =
                let s1 = (x.GetColumn s)
                s1.Replace( ',', '.' )

            let readFloat (s) =
                try
                    System.Single.Parse( readAndReplace s , CultureInfo.InvariantCulture )
                with 
                | e -> 
                    printfn "%A Warning: %A, col: %A str: %A" i e.Message s (x.GetColumn s)
                    0.0f

            let readUInt64 (s:string) = 
                try
                    System.UInt64.Parse (x.GetColumn s)
                with 
                | e -> 
                    printfn "%A Warning: %A, col: %A str: %A" i e.Message s (x.GetColumn s)
                    0UL

//                    printf "i: %d: " i

// Recording timestamp	Computer timestamp	Pupil diameter left	Pupil diameter right	Pupil diameter avg	Validity left	Validity right	Validity both	Raw Blink left	HershBlink left	Raw Blink right	HershBlink right	Raw Blink both	HershBlink avg

            let recording = readUInt64 "Recording timestamp"
            let timeStamp = readUInt64 "Computer timestamp"
            let pupilLeft = readFloat "Pupil diameter left"
            let pupilRight = readFloat "Pupil diameter right"
            let pupilAvg = readFloat "Pupil diameter avg"
            
            let valid  = x.GetColumn "Validity both"
            let validL = x.GetColumn "Validity left"
            let validR = x.GetColumn "Validity right"

            let blinkRawLeft = readUInt64 "Raw Blink left"
            let blinkRawRight = readUInt64 "Raw Blink right"
            let blinkRawBoth= readUInt64 "Raw Blink both" 
            
            sprintf "%d\t%d\t%.3f\t%.3f\t%.3f\t%s\t%s\t%s\t%d\t%d\t%d"
                recording
                timeStamp
                pupilLeft
                pupilRight
                pupilAvg
                valid
                validL
                validR
                blinkRawLeft
                blinkRawRight
                blinkRawBoth
            )
        |> Seq.toArray

    let fileNameExt = Path.GetFileName(resFile)
    let fullPath =  Path.GetFullPath(resFile)
    let path = fullPath.Substring(0, fullPath.Length - fileNameExt.Length)

    data
    |> Array.splitInto(splits)
    |> Seq.iteri( fun i x -> 

        let splitPath = Path.Combine( path, "Split" + string(i+1) )
        Directory.CreateDirectory(splitPath) |> ignore

        let fileName = 
            Path.Combine( 
                splitPath, 
                Path.GetFileNameWithoutExtension(resFile) + "_S" + string(i+1) + 
                Path.GetExtension(resFile) ) 
    
        if File.Exists( fileName ) then File.Delete( fileName )

        let linesWithHeader = Array.concat [| 
            [|"Recording timestamp\tComputer timestamp\tPupil diameter left\tPupil diameter right\tPupil diameter avg\tValidity left\tValidity right\tValidity both\tRaw Blink left\tRaw Blink right\tRaw Blink both" |]
            x |]

        File.WriteAllLines(fileName, linesWithHeader)
        )
    ()

// blink rate
// * nach time-label
// * bis zum nachstes time-event oder fixe dauer
// * blinks pro minute
// * blink duration pro minute
// * blink time 
// * column in blink table of last time-label