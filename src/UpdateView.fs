module UpdateView

open System.Windows.Forms
open UiState
open Plot
open OxyPlot

let stateDo (action) =
    match UiState.state with
    | None -> ()
    | Some s -> action s

let updateState (update) =
    // printfn "updateState"
    stateDo <| fun s ->
        let s = update s
        UiState.state <- Some s

let getPlotData (s) =
    let vTargets     = s.Session.Targets.ChangesValid
    let evalData, intervalData, blinkData, targetAnalysisData = reAnalyze s.TargetNr s

    if Seq.isEmpty evalData then
        None 
    else

    let delta = {x= 0.0; y=30.0}
    let saccsMinMax = 
        ControlTheorie.getSaccades intervalData targetAnalysisData
        |> List.map( fun x -> x + delta )

    let saccsData=
        saccsMinMax
        |> Seq.map (ControlTheorie.getData evalData)
        |> Seq.map (ControlTheorie.fitFunction)
        |> Seq.filter (fun x -> x.IsSome )
        |> Seq.map (fun x -> ControlTheorie.computeAxisCrds s.Session evalData x.Value )

//            saccsData
//            |> Seq.iteri( fun i x -> (ControlTheorie.computeAxisCrds s.Session evalData x) |> ignore ) 

    let firstTime = (evalData |> Seq.head).Time.TimeStamp
    let lastTime = (evalData |> Seq.last).Time.TimeStamp
    
    let timeRange = Some {Start= firstTime; End= lastTime}
    updateState (setXAxisMinMax timeRange)

    let mutable timeCommentIdx = -1
    let mutable lastIdx = -1;

    s.Session.TimeComment
    |> Array.indexed
    |> Array.filter( fun (i, (k,v)) -> v.Chars(0) = '+' )
    |> Array.iter( fun (i, (k,v)) -> 
//                printfn "%A, %A" k v
        if k > firstTime && timeCommentIdx = -1 && v.Chars( 0 ) = '+' then
//                    printfn "  Chosen!"
            timeCommentIdx <- lastIdx
        else
            lastIdx <- i )
         
    let timeComment =  
        if timeCommentIdx > 0 then
            snd s.Session.TimeComment.[timeCommentIdx]
        else if timeCommentIdx = 0 then
            snd s.Session.TimeComment.[0]
        else
            "-"
        
    let timeComments = 
        s.Session.TimeComment
        |> Array.filter( fun (k,_) -> k >= firstTime && k <= lastTime)
          
    let targetStart = s.Session.EyeData.[fst vTargets.[s.TargetNr]].timeTarget.TimeStamp

    let targetLabel = 
        if s.Session.Targets.Labels.IsSome then 
            let _, txt, _ = s.Session.Targets.Labels.Value.[s.TargetNr]
            txt
        else "-"
  
    Some {
        targetNr= fst vTargets.[s.TargetNr]
        target = snd vTargets.[s.TargetNr]
        targetLabel = targetLabel
        currNr = s.TargetNr 
        currSize = vTargets.Length
        isError = (ETReaction.getErrorNr s.TargetNr s.Session.Errors)
        data = evalData
        xZoom = timeRange
        saccs = saccsMinMax
        saccsData = saccsData
        config = s.PlotConfig
        timeComments = timeComments
        intervals = intervalData
        targetStart = targetStart
        blinkIntervals = blinkData
        userCorr = s.Session.UserCorrections
        analysis = targetAnalysisData
    }

let updateWindowTitle (win:Form) (session:ETData.Session) = 
    let prefix = 
        let toks = session.Path.Split( [|'/'; '\\'|] )
        if toks.Length > 0 then 
            toks.[toks.Length-1]
        else
            session.Path.Substring( max 0 (session.Path.Length-16) )

    win.Text <- "Target Event View: "  +  prefix + "/" + session.Name


let updatePlotGaze (gazePlotFigure) (gazePlotData) =
    Plot.updateGazeModel gazePlotFigure gazePlotData
    gazePlotFigure

let updatePlotBlink (blinkPlotFigure) =
    Plot.updateBlinks blinkPlotFigure [||]

let updateView (view:WindowsForms.PlotView) (model:PlotModel) =
    view.Model <- model
    view.Show()

let updateGaze (gazePlotFigure) (gazePlotView) (s)  =
    // printfn "updateGaze"
    let gazePlotData = getPlotData s

    if gazePlotData.IsSome then
        gazePlotData.Value
        |> updatePlotGaze gazePlotFigure
        |> updateView gazePlotView

let rec updatePlotTargets 
    (allTargetsPlotFigure) (allTargetsPlotView)
    (gazePlotFigure) (gazePlotView)
    (blinkPlotFigure) (blinkPlotView)
    (s:UiState.T) = 
    let vTargets     = s.Session.Targets.ChangesValid
    
    let mouseCallBack (i) =
        updateState (UiState.setTargetNr i)
        stateDo (updateGaze gazePlotFigure gazePlotView)
        stateDo (updatePlotTargets  allTargetsPlotFigure allTargetsPlotView gazePlotFigure gazePlotView 
        blinkPlotFigure blinkPlotView)
        updateView allTargetsPlotView allTargetsPlotFigure

        Plot.updateBlinks blinkPlotFigure UiState.state.Value.Session.BlinkData UiState.state.Value.XAxisMinMax
        updateView blinkPlotView blinkPlotFigure
      

    Plot.updateAllTargets allTargetsPlotFigure vTargets mouseCallBack 

let updateAllViews 
    (gazePlotView) (gazePlotFigure) 
    (blinkPlotView) (blinkPlotFigure)
    (allTargetsPlotView) (allTargetsPlotFigure)
    =
    updateView gazePlotView gazePlotFigure
    updateView blinkPlotView blinkPlotFigure
    updateView allTargetsPlotView allTargetsPlotFigure
