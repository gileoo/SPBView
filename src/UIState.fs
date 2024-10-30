module UiState // 100

type T = {
    Session      : ETData.Session
    TargetNr     : int
    FilterTarget : string list
    ClickPosMode : bool
    WarpPosition : vec2
    LastClickX   : float
    MousePosition : vec2
    PlotWidth    : float
    XAxisMinMax  : Range<float> option
    SkipErrors   : bool
    PlotConfig   : string list}

let mutable (state: T option) = None

let create (plotConfig) (skipErrors) (stateTargetNr) (session) = 
    {
        Session      = session
        TargetNr     = if session.Targets.LastIdx < stateTargetNr then 0 else stateTargetNr
        FilterTarget = []
        ClickPosMode = false
        WarpPosition = { x=0.0; y=0.0 }
        LastClickX   = 0.0 
        MousePosition = { x=0.0; y=0.0 }
        PlotWidth = 0.0
        XAxisMinMax  = None
        SkipErrors   = skipErrors
        PlotConfig   = plotConfig
    }

let togglePlotConfig (s:string) (state)= 
    if state.PlotConfig |> List.contains s then           
        { state with PlotConfig = state.PlotConfig |> List.filter( fun x -> x <> s  ) }
    else
        { state with PlotConfig = state.PlotConfig @ [s] }

let toggleSkipError state = { state with SkipErrors = not state.SkipErrors }
let updateSession (update) (state) = { state with Session = update state.Session }
let setClickPosMode (mode) (state) = { state with ClickPosMode = mode }
let setXAxisMinMax (xAxisMiMa: Range<float> option) (state) = { state with XAxisMinMax = xAxisMiMa  }

let setMousePosition (pos) (state) = { state with MousePosition = pos }
let setPlotWidth (width) (state) = { state with PlotWidth = width }
let setWarpPosition (pos) (state) = { state with WarpPosition = pos }

let setBlinkData (bData) (state) = 
    { state with Session= { state.Session with BlinkData= bData } }

let reAnalyze (targetNr) (state) =
    let vTargets     = state.Session.Targets.ChangesValid
    let evalData     = ETReaction.evalTargetEvents state.Session targetNr
    if vTargets.Length = 0 || Seq.isEmpty evalData then
        (Seq.empty, List.empty, List.empty, List.empty)
    else
    let intervalData = ETReaction.baseIntervalsFromTarget evalData state.Session.DataConfig vTargets.[targetNr]
    let blinkData    = ETReaction.blinkIntervalsFromTarget evalData vTargets.[targetNr]
    let target       = snd vTargets.[targetNr]
    let targetStart  = state.Session.EyeData.[fst vTargets.[targetNr]].timeTarget.TimeStamp
    let targetAnalysisData = 
        ETReaction.analyseIntervalsForTarget 
            intervalData 
            (ETReaction.getErrorNr state.TargetNr state.Session.Errors) 
            targetNr 
            target.FullName
            targetStart
    (evalData, intervalData, blinkData, targetAnalysisData)

let setTargetNr newTarget state =
    { state with
        TargetNr    = newTarget |> max 0 |> min (state.Session.Targets.ChangesValid.Length - 1)
        XAxisMinMax = None }


let private indexOfFirstValid (filter:int seq -> int seq) (state) =
    Seq.init state.Session.Targets.ChangesValid.Length id
    |> filter
    |> Seq.where (fun idx -> not <| state.Session.Errors.ContainsKey idx)
    |> Seq.where
        (fun idx ->
            (snd state.Session.Targets.ChangesValid.[idx]).FullName
            |> ETReaction.isDesiredTarget state.FilterTarget
        )
    |> Seq.where
        (fun (idx) ->
            let _, _, _, targetAnalysisData = reAnalyze idx state 
            not targetAnalysisData.IsEmpty
            && targetAnalysisData.Head.error <> 10
        )
    |> Seq.tryHead

let previousValidTarget (state:T) =
    state
    |> indexOfFirstValid (Seq.rev << Seq.where (fun idx -> idx < state.TargetNr))
    |> Option.map (fun idx -> setTargetNr idx state)
    |? state


let rec changeNrToNextFilteredTarget (state:T) (idx:int) (f:int->int)= 
    if idx > (state.Session.Targets.ChangesValid.Length - 1) || 
       idx < 0 then idx else

    let target = snd state.Session.Targets.ChangesValid.[idx]

    let doFilter = 
        state.FilterTarget
        |> List.filter( fun x -> target.FullName.Contains x)

    if doFilter.Length > 0 then
        idx
    else
        changeNrToNextFilteredTarget state (f idx) f


let nextTarget (state:T) = 
    if state.FilterTarget.IsEmpty then
        setTargetNr (state.TargetNr + 1) state
    else
        let newIdx = changeNrToNextFilteredTarget state (state.TargetNr+1) (fun x -> x+1)
        setTargetNr newIdx state

let previousTarget (state) =
    if state.FilterTarget.IsEmpty then
        setTargetNr (state.TargetNr - 1) state
    else
        let newIdx = changeNrToNextFilteredTarget state (state.TargetNr-1) (fun x -> x-1)
        setTargetNr newIdx state

let gotoTarget (state) (nr) = 
    setTargetNr nr state

let nextValidTarget (state) =
    state
    |> indexOfFirstValid (Seq.where (fun idx -> idx > state.TargetNr))
    |> Option.map (fun idx -> setTargetNr idx state)
    |? state

let firstTarget (state) = setTargetNr 0 state

let markTarget (errorIdx) (state) =
    Map.add state.TargetNr errorIdx
    |> ETData.Session.updateErrors
    |> updateSession <| state

let addReactionTimeToCurrentTarget (minX) (clickX) (state) =
    match state.ClickPosMode with
    | false ->
        {state with
            LastClickX = max minX clickX
        }
    | true ->
        let reactTimeStamps =
            let P = max minX clickX

            if state.LastClickX < P
            then { x = round state.LastClickX; y = round P                }
            else { x = round P               ; y = round state.LastClickX }

        Map.add state.TargetNr reactTimeStamps
        |> ETData.UserCorrectionData.updateReactionTime
        |> ETData.Session.updateUserCorrections
        |> updateSession <| state
