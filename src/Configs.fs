module Configs

open System.IO
open System
open System.Collections.Generic
open System.Globalization
open FSharp.Data

let cfgPath = 
    Path.GetDirectoryName(
        Uri(System.Reflection.Assembly.GetExecutingAssembly()
        .EscapedCodeBase ).AbsolutePath )

let fileNameIni = "SPBView.ini"
let fileNameExpSetup = "ExperimentSetup.conf"
let fileNameInputData = "InputData.conf"
let fileNameEyeTargets = "EyeTargets.conf"
let fileNameMedia = "Media.conf"

let filePathIni = Path.Combine(cfgPath, fileNameIni)
let filePathExpSetup = Path.Combine(cfgPath, fileNameExpSetup)
let filePathInputData = Path.Combine(cfgPath, fileNameInputData)
let filePathEyeTargets = Path.Combine(cfgPath, fileNameEyeTargets)
let filePathMedia = Path.Combine(cfgPath, fileNameMedia)


// -- Internal Variable Names (column variable as key in the InputData.conf file)
module CV =
    let ReactionTimeStart = "ReactionTimeStart"
    let ReactionTimeStop = "ReactionTimeStop"
    let TargetNr = "TargetNr"
    let RecordingTimestamp = "RecordingTimestamp"
    let PupilDiameterLeft = "PupilDiameterLeft"
    let PupilDiameterRight = "PupilDiameterRight"
    let GazePointX = "GazePointX"
    let GazePointY = "GazePointY"
    let GazePointLeftX = "GazePointLeftX"
    let GazePointLeftY  = "GazePointLeftY"
    let GazePointRightX = "GazePointRightX"
    let GazePointRightY = "GazePointRightY"
    let ValidityLeft = "ValidityLeft"
    let ValidityRight = "ValidityRight"
    let EyePositionLeftX = "EyePositionLeftX"
    let EyePositionLeftY = "EyePositionLeftY"
    let EyePositionLeftZ = "EyePositionLeftZ"
    let EyePositionRightX = "EyePositionRightX"
    let EyePositionRightY = "EyePositionRightY"
    let EyePositionRightZ = "EyePositionRightZ"
    let EyeMovementType = "EyeMovementType"
    let GazeEventDuration = "GazeEventDuration"
    let Event = "Event"
    let PresentedMediaName = "PresentedMediaName"
    let AoiHitCueR_Rectangle2 = "AoiHitCueR_Rectangle2"
    let ParticipantName = "ParticipantName"
    let ErrorType = "ErrorType"

// -- Flag Names
module FN =
    let TargetType  = "StatesTargetType"
    let Experiment  = "StatesExperiment"
    let Target      = "StatesTarget"
    let EyeMovement = "StatesEyeMovement"
    

let cols = [| 
    CV.ParticipantName
    CV.ReactionTimeStart
    CV.ReactionTimeStop
    CV.TargetNr
    CV.RecordingTimestamp
    CV.PupilDiameterLeft
    CV.PupilDiameterRight
    CV.GazePointX
    CV.GazePointY
    CV.GazePointLeftX
    CV.GazePointLeftY
    CV.GazePointRightX
    CV.GazePointRightY
    CV.ValidityLeft
    CV.ValidityRight
    CV.EyePositionLeftX
    CV.EyePositionLeftY
    CV.EyePositionLeftZ
    CV.EyePositionRightX
    CV.EyePositionRightY
    CV.EyePositionRightZ
    CV.EyeMovementType
    CV.GazeEventDuration
    CV.Event
    CV.PresentedMediaName
    CV.AoiHitCueR_Rectangle2
    CV.ErrorType |]

let flags = [|
    FN.TargetType
    FN.Experiment
    FN.Target
    FN.EyeMovement |]

type Setup = {
    Xk : float32 
    Xd : float32 
    Yk : float32
    Yd : float32
    Device : string
}

type ProgramCfg = {
    OpenPath : string
    SavePath : string
    PlotConfig : string list
    StateSkipError : bool
    StateTargetNr : int32
    GuiTargetFilter : string
}

type DataCfg = {
    ColsNamesCSV : Dictionary<string, string>
    StateTargetType : string list
    StateExperiment : string list
    StateTarget : string list
    StateEyeMovement : string list
    }

type Config = {
    ProgramState : ProgramCfg 
    ExpSetup : Setup
    InputData : DataCfg
    // eye targets
}

let readIniFile() =

    let mutable plotConfig = List.empty
    let mutable openPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal)
    let mutable savePath = Environment.GetFolderPath(Environment.SpecialFolder.Personal)
    let mutable stateSkipError = false  
    let mutable stateTargetNr = 0
    let mutable guiTargetFilter = ""

    try
    
    let readLines = File.ReadLines( filePathIni )

    readLines 
    |> Seq.iter( fun x ->
        let toks = x.Split('=')
        let appendPltCfg s = 
            if toks.[1].Trim() = "true" then 
                plotConfig <- plotConfig @ [s]

        if toks.Length = 2 then
            match toks.[0].Trim() with
            | "OpenPath"   -> openPath <- toks.[1].Trim()
            | "SavePath"   -> savePath <- toks.[1].Trim()
            | "SkipErrors" -> stateSkipError <- if toks.[1].Trim() = "true" then true else false
            | "TargetNr"   -> Int32.TryParse( toks.[1].Trim(), &stateTargetNr ) |> ignore
            | "TargetFilter" -> guiTargetFilter <- toks.[1].Trim()
            | "PlotSpeed"   -> appendPltCfg "Speed"
            | "PlotXError"  -> appendPltCfg "XError"
            | "PlotYError"  -> appendPltCfg "YError"
            | "PlotPupilSize" -> appendPltCfg "PupilSize"
            | "PlotEError"  -> appendPltCfg "EError"
            | "PlotIntervals"     -> appendPltCfg "Intervals"
            | "PlotLRegression"   -> appendPltCfg "LRegression"
            | "PlotNLRegression"  -> appendPltCfg "NLRegression"
            | "PlotAInError" -> appendPltCfg "AInError"
            | "PlotAFromError" -> appendPltCfg "AFromError"
            | "PlotAFullError" -> appendPltCfg "AFullError"
            | _ -> printfn "Unknown config" )

    if plotConfig.Length = 0 then
        plotConfig <- plotConfig @ ["Speed"]
        plotConfig <- plotConfig @ ["XError"]
        plotConfig <- plotConfig @ ["Intervals"]
    
    with
    | :? FileNotFoundException -> 
        printfn "no SPBView.ini file found, using defaults"
        // failwith (sprintf "file not found: %s" filePathIni)
    
    if plotConfig.Length = 0 then
        plotConfig <- plotConfig @ ["Speed"]
        plotConfig <- plotConfig @ ["XError"]
        plotConfig <- plotConfig @ ["Intervals"]

    {   OpenPath = openPath
        SavePath = savePath
        PlotConfig = plotConfig
        StateSkipError = stateSkipError
        StateTargetNr = stateTargetNr
        GuiTargetFilter = guiTargetFilter }

let readExpSetupFile() =
    try    
    let readLines = File.ReadLines( filePathExpSetup )
    let mutable xk = 0.0f
    let mutable xd = 0.0f
    let mutable yk = 0.0f
    let mutable yd = 0.0f
    let mutable device = ""

    readLines 
    |> Seq.iteri( fun i x ->
        let toks = x.Split('=')
        if toks.Length = 2 then
            match toks.[0].Trim() with
            | "xk"   -> xk <- Single.Parse( toks.[1].Trim(), CultureInfo.InvariantCulture )
            | "xd"   -> xd <- Single.Parse( toks.[1].Trim(), CultureInfo.InvariantCulture )
            | "yk"   -> yk <- Single.Parse( toks.[1].Trim(), CultureInfo.InvariantCulture )
            | "yd"   -> yd <- Single.Parse( toks.[1].Trim(), CultureInfo.InvariantCulture )
            | "Device"   -> device <- toks.[1].Trim()
            | _ -> printfn "Unknown config" )
    
    {   Xk = xk 
        Xd = xd 
        Yk = yk
        Yd = yd
        Device = device }

    with
    | :? FileNotFoundException -> failwith (sprintf "file not found: %s" filePathExpSetup)



let readColumnsAndStatesNames() =
    try

    let mutable colsCSV = new Dictionary<string, string>()
    let mutable stateTargetType = List.empty
    let mutable stateExperiment = List.empty
    let mutable stateTarget = List.empty
    let mutable stateEyeMovement = List.empty

    let readLines = File.ReadLines( filePathInputData )

    readLines 
    |> Seq.iter( fun x ->
        let toks = x.Split('=')
    
        if toks.Length = 2 then
            let colName = toks.[0].Trim()
    
            if cols |> Array.contains colName then
                let column = 
                    cols
                    |> Array.find( fun x -> colName = x )
    
                colsCSV.Add( column, toks.[1].Trim() )
    
            elif flags |> Array.contains colName then
                toks.[1].Split(';')
                |> Array.iter (fun t ->
                    match colName with 
                    | "StatesTargetType"  -> stateTargetType <- stateTargetType @ [ t.Trim() ]
                    | "StatesExperiment"  -> stateExperiment <- stateExperiment @ [ t.Trim() ]
                    | "StatesTarget"      -> stateTarget <- stateTarget @ [ t.Trim() ]
                    | "StatesEyeMovement" -> stateEyeMovement <- stateEyeMovement @ [ t.Trim() ]
                    | _             -> failwith "Unsupported flag name in InputData.cfg")
            else
                failwith (sprintf "Unsupported data column in InputData.cfg '%s'" colName)
                                   
        else 
            failwith "Corrupted data column in InputData.cfg" 
    )
    
    printfn "Read Configuration Cols: %A" colsCSV
    printfn "Read Configuration Flag TargetType: %A" stateTargetType
    printfn "Read Configuration Flag Experiment: %A" stateExperiment
    printfn "Read Configuration Flag Target: %A" stateTarget
    printfn "Read Configuration Flag EyeMovement: %A" stateEyeMovement
    
    {   ColsNamesCSV = colsCSV
        StateTargetType = stateTargetType
        StateExperiment = stateExperiment
        StateTarget = stateTarget
        StateEyeMovement = stateEyeMovement }

    with 
    | :? FileNotFoundException -> 
        failwith (sprintf "%s file not found" filePathInputData)
        // TODO: load defaults here
    


let GlobalCfg = 
    {
        ProgramState = readIniFile()
        ExpSetup = readExpSetupFile()
        InputData = readColumnsAndStatesNames()
    }
