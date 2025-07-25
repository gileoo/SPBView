﻿module Csv // 293

open System
open System.IO
open FSharp.Data
open System.Globalization
open System.Collections.Generic
open ETData
open ETReaction
open Configs

let cleanHashCommentsFromASCIIFile filePath =
    let pre = Path.GetFullPath(filePath).Split('.').[0]
    let cleanPath = pre  + "_c" + Path.GetExtension(filePath)

    if (File.Exists(cleanPath)) || filePath.Contains("_c") then
        filePath
    else 
        let cleanedLines = 
            File.ReadLines (filePath)
            |> Seq.filter( fun x -> not (x.Trim().[0] = '#'))
        File.WriteAllLines (cleanPath, cleanedLines)
        cleanPath

let getCol s = 
    if GlobalCfg.InputData.ColsNamesCSV.ContainsKey s then
        let ret = GlobalCfg.InputData.ColsNamesCSV.TryGetValue s 
        (snd ret).Trim()
    else if GlobalCfg.InputData.ConstColsNamesCSV.ContainsKey s then
        let ret = GlobalCfg.InputData.ConstColsNamesCSV.TryGetValue s 
        (snd ret).Trim()
    else
        ""

let private readFloatColumn (s:string) (r:CsvRow) =
    let s1 = (r.GetColumn (getCol s)).Replace( ',', '.' )
    try
        System.Single.Parse( s1 , CultureInfo.InvariantCulture )
    with 
    | e -> 
        printfn "Exception: %A, col: %A str: %A" e.Message s (r.GetColumn s)
        0.0f

// -- old function to test CsvReader on a tsv file, printing data of every 100th line
let showSomeOfFile (file:CsvFile) =
    file.Rows
    |> Seq.iteri (fun i x ->
        let getColumn c = x.GetColumn (getCol c)

        let getColString (s:string) =
            getColumn s
            |> String.makeEmptyVisible

        if ( i % 100 = 0) then 
            printfn "Count: %A;\n RecTimestamp: %s; PupilDiameterL: %s; PupilDiameterR: %s; GazeX: %s; GazeY: %s; ValidityL: %s; ValidityR: %s;\n  EyePosLX: %s; EyePosLY: %s; EyePosLZ: %s; EyePosRX: %s; EyePosRY: %s; EyePosRZ: %s\n  EyeMovementType: %s; GazeEventDuration: %s; Event: %s; MediaName: %s; AOIhit: %s" 
                i
                (getColString CV.RecordingTimestamp) 
                (getColString CV.PupilDiameterLeft) 
                (getColString CV.PupilDiameterRight) 
                (getColString CV.GazePointX)
                (getColString CV.GazePointY)
                (getColString CV.ValidityLeft)
                (getColString CV.ValidityRight)
                (getColString CV.EyePositionLeftX)
                (getColString CV.EyePositionLeftY)
                (getColString CV.EyePositionLeftZ)
                (getColString CV.EyePositionRightX)
                (getColString CV.EyePositionRightY)
                (getColString CV.EyePositionRightZ)
                (getColString CV.EyeMovementType)
                (getColString CV.GazeEventDuration)
                (getColString CV.Event)
                (getColString CV.PresentedMediaName)
                (getColString CV.AoiHitCueR_Rectangle2)
    )

type labelStates = 
    {
        Mood        : string[]
        Experiment  : string[]
        Target      : string[]
        EyeMovement : string[]  
    }

// -- data reading function
//    generates and fills the eye tracking data types
//    returns an array of EyesSnapshots, a count of invalid lines, 
//    and an array of target-file names

let clearMediaName (mName:string) =
    if mName.Contains("(") || mName.Contains(".") then
        let idx = mName.IndexOfAny([|'('; '.'|])
        mName.Substring(0, idx)
    else
        mName

let private getFileAsArrayMediaChange (file:CsvFile) (mediaToTarget:Dictionary<string, (string * int)>)=
    let mutable countInvalids = 0
    let mutable media = Map.empty

    let mutable patientName = ""
    let mutable timeComments = List.empty

    let clearedMediaToTarget (key) =
        let clearedKey = clearMediaName key
        if mediaToTarget.ContainsKey(clearedKey) then
            snd (mediaToTarget.TryGetValue clearedKey)
        else
           ("", -1)

    let data =
        file.Rows
        |> Seq.mapi( fun i x -> 
            if i % 500 = 0 then printf "."
            let getColumn c = 
                let mappedColName = (getCol c)
                //printfn "getting: %s -> %s" c mappedColName 
                if mappedColName = "" then 
                    failwith (sprintf "unsupported column header '%s' -> '%s'" c mappedColName)
                if mappedColName.Contains("user-const") then
                    mappedColName.Substring(10)
                else
                    try
                        x.GetColumn mappedColName
                    with 
                    | e -> failwith (sprintf "Column header not found in input file: %s" mappedColName)

            let readAndReplace (s:string) =
                let s1 = (getColumn s)
                s1.Replace( ',', '.' )

            let readFloat (s) =
                try
                    System.Single.Parse( readAndReplace s , CultureInfo.InvariantCulture )
                with 
                | e -> 
                    #if DEBUG
                    printfn "%A Exception: %A, col: %A str: %A" i e.Message s (getColumn s)
                    #endif
                    0.0f

            let readInt (s) =
                try
                   let colVal = getColumn s
                   System.Int32.Parse( colVal )
                with 
                | e -> 
                    #if DEBUG
                    printfn "%A Exception: %A, col: %A str: %A" i e.Message s (getColumn s)
                    #endif
                    0

            let isValid (s:string) =
                let validityStr = s.ToLower()
                (validityStr = "1" || validityStr = "valid")

            let timeStamp = float (System.UInt64.Parse (getColumn CV.RecordingTimestamp))

            let eventLabel = getColumn CV.Event
            if eventLabel <> "" then
                timeComments <- List.append timeComments [(timeStamp,eventLabel)]

            // check for media file
            let mediaName = getColumn CV.PresentedMediaName
            let mediaNr =
                match media.TryFind mediaName with
                | Some x -> 
                    x
                | None -> 
                    let id = media.Count
                    media <- Map.add mediaName id media 
                    id

            let targetName = clearedMediaToTarget mediaName

            let timeTarget = 
                {
                    TimeStamp = timeStamp
                    MediaNr = mediaNr 
                    TargetName = fst targetName
                    TargetNr = snd targetName
                }    

            let str = readAndReplace CV.EyePositionLeftX
            let mvType = getColumn CV.EyeMovementType

            if str <> "" && mvType <> "EyesNotFound" then
                if patientName = "" then
                    patientName <- getColumn CV.ParticipantName

                let X = readFloat CV.EyePositionLeftX
                let Y = readFloat CV.EyePositionLeftY
                let Z = readFloat CV.EyePositionLeftZ
                let valid = isValid (getColumn CV.ValidityLeft)
                let dia = readFloat CV.PupilDiameterLeft

                if X = 0.0f && Y = 0.0f then
                    countInvalids <- countInvalids + 1
                    { timeTarget= timeTarget; eyes= EyesData.Zero; comment = "" }
                else
                    let left = 
                        { 
                        Position={ x=X; y=Y; z=Z }
                        GazePoint= { x=readFloat CV.GazePointLeftX
                                     y=readFloat CV.GazePointLeftY
                                     z=0.0f }  
                        Valid=valid
                        PupilDiameter=dia 
                        }
         
                    let right = 
                        { 
                        Position={ x=readFloat CV.EyePositionRightX
                                   y=readFloat CV.EyePositionRightY
                                   z=readFloat CV.EyePositionRightZ }
                        GazePoint = { x=readFloat CV.GazePointRightX
                                      y=readFloat CV.GazePointRightY
                                      z=0.0f } 
                        Valid=isValid (getColumn CV.ValidityRight)
                        PupilDiameter=readFloat CV.PupilDiameterRight
                        }      

                    let eyes = 
                        { 
                            Left = left
                            Right = right
                            GazePointPixel = { i=readInt CV.GazePointX 
                                               j=readInt CV.GazePointY }
                            MovementType = (getColumn CV.EyeMovementType)
                            GazeDuration = (float) (readInt CV.GazeEventDuration)
                            Event = (match getColumn CV.EyeMovementType with 
                                     | "Fixation" -> EyeEvent.Fixation 
                                     | x -> EyeEvent.Unknown x)
                        } 

                    { timeTarget= timeTarget; eyes= eyes; comment = if eventLabel <> "" then "userEvent" else "" }
            else
                { timeTarget= timeTarget; eyes= EyesData.Zero; comment = if eventLabel <> "" then "userEvent" else "" }
            )
        |> Seq.filter( fun x -> x.comment <> "userEvent")
        |> Seq.toArray

    let mediaArr = Array.init media.Count (fun x -> "" )
    media
    |> Map.iter (fun k v -> 
        let mediaName, mediaNr = clearedMediaToTarget k
        if mediaNr > -1 then
            mediaArr.[v] <- sprintf "%s (%d) %s" mediaName mediaNr k
        else
            mediaArr.[v] <- k
        )

    let timeLabels =
        timeComments
        |> List.sortBy( fun (x,_) -> x )
        |> List.toArray

    (data, countInvalids, mediaArr, patientName, timeLabels)

// -- data reading function
//    generates and fills the eye tracking data types
//    returns an array of EyesSnapshots, a count of invalid lines, 
//    and an array of target-file names
let private getFileAsArrayTimedLabels (file:CsvFile)=
    let mutable countInvalids = 0
    let mutable media = Map.empty

    let mutable patientName = ""

    let mutable lastStateExperiment = ""
    let mutable lastStateEyeMovement = ""
    let mutable lastStateTargetType = ""
    let mutable lastStateTarget = ""

    let mutable timeComments = List.empty

    let mutable lastStart = 0.0
    let mutable lastEvent = 0.0
    let mutable expectColor = false

    let mutable catchCurrent = false
    let mutable lastNr = -1

    let data =
        file.Rows
        |> Seq.mapi( fun i x -> 
            if i % 500 = 0 then printf "."
            //let getColumn c = x.GetColumn (getCol c)
            let getColumn c = 
                let mappedColName = (getCol c)
                // printfn "getting: %s -> %s" c mappedColName 
                if mappedColName = "" then 
                    failwith (sprintf "unsupported column header %s" mappedColName)
                if mappedColName.Contains("user-const") then
                    mappedColName.Substring(10)
                else
                    try
                        x.GetColumn mappedColName
                    with 
                    | e -> failwith (sprintf "Column header not found in input file: %s" mappedColName)

            let readAndReplace (s:string) =
                let s1 = getColumn s
                s1.Replace( ',', '.' )

            let readDouble (s) = 
                try
                    System.Double.Parse( readAndReplace s , CultureInfo.InvariantCulture )
                with 
                | e -> 
                    #if DEBUG
                    printfn "%A Exception: %A, col: %A str: %A" i e.Message s (getColumn s)
                    #endif
                    0.0

            let readFloatNeg1 (s) =
                try
                    System.Single.Parse( readAndReplace s , CultureInfo.InvariantCulture )
                with 
                | e -> 
                    -1.0f

            let readInt32Neg1 (s:string) =
                try
                    System.Int32.Parse( getColumn s )
                with 
                | e -> 
                    -1

            let eventLabel = getColumn "Event"

            let timeStamp = readDouble CV.RecordingTimestamp

            if GlobalCfg.InputData.StateExperiment |> List.contains eventLabel then
                lastStateExperiment <- eventLabel
                lastStateTarget  <- eventLabel
                timeComments <- List.append timeComments [(timeStamp,eventLabel)]
                catchCurrent <- true
                if eventLabel.ToLower() = "start" then
                    lastStart <- timeStamp
                    expectColor <- true
            else if GlobalCfg.InputData.StateTargetType |> List.contains eventLabel then
                lastStateTargetType <- eventLabel
                timeComments <- List.append timeComments [(timeStamp, "+" + eventLabel)]
            else if GlobalCfg.InputData.StateTarget |> List.contains eventLabel then
                lastStateTarget <- eventLabel
                timeComments <- List.append timeComments [(timeStamp,eventLabel)]
                catchCurrent <- true
            else if GlobalCfg.InputData.StateEyeMovement |> List.contains eventLabel then
                lastStateEyeMovement <- eventLabel
                timeComments <- List.append timeComments [(timeStamp,eventLabel)]

            //if expectColor && timeStamp - lastStart >= 1000.0 then
            //    expectColor <- false
            //    timeComments <- List.append timeComments [(timeStamp,"colorChange")]

            let currentNr = 
                lastNr <-
                    if catchCurrent then
                        catchCurrent <- false
                        lastEvent <- timeStamp
                        (*
                        match (lastStateEyeMovement, lastStateTarget) with
                        | ("anti", "leftTarget")  -> 1
                        | ("pro",  "leftTarget")  -> 2
                        | ("anti", "rightTarget") -> 3
                        | ("pro",  "rightTarget") -> 4
                        | _ -> 0   
                        *)

                        (*
                        match lastStateTarget with
                        | "leftTarget"  -> 1
                        | "rightTarget" -> 2
                        | "Trial Start"
                        | "Target Off"  -> 3
                        | _ -> 0  
                        *)

                        let inputTargetArr =
                            GlobalCfg.InputData.StateTarget 
                            |> List.toArray

                        let targetIdx = 
                            inputTargetArr
                            |> Array.tryFindIndex (fun x -> x = lastStateTarget) 
 
                        let idx = 
                            match targetIdx with
                            | Some x -> x
                            | None -> GlobalCfg.InputData.StateTarget.Length - 1

                        idx
                    else                        
                        if timeStamp - lastEvent <= 1000.0 then
                            lastNr
                        else
                            0
                lastNr

            // check for media file
            let mediaName = lastStateTarget
            let mediaNr   = 
            //    if timeStamp - lastStart  < 2000.0 then
            //        catchCurrent <- true
            //        0
            //    else
                    currentNr 

            let timeTarget = 
                {
                    TimeStamp = timeStamp
                    MediaNr   = mediaNr 
                    TargetName = mediaName
                    TargetNr = mediaNr
                }    

//            let str = readAndReplace CV.EyePositionLeftX // let mvType = getColumn CV.EyeMovementType

            let X = -1.0f
            let Y = -1.0f
            let Z = -1.0f
            
            let validStr = getColumn CV.ValidityLeft
            let valid = (validStr = "1")
            //printfn "[%d] validStr: %A, val %A" i validStr valid

            let left = 
                { 
                Position={ x=readFloatNeg1 CV.EyePositionLeftX
                           y=readFloatNeg1 CV.EyePositionLeftY
                           z=readFloatNeg1 CV.EyePositionLeftZ }
                GazePoint = pxToMm (readInt32Neg1 CV.GazePointLeftX)  (readInt32Neg1 CV.GazePointLeftY)
                Valid=valid
                PupilDiameter=readFloatNeg1 CV.PupilDiameterLeft 
            }
         
            let right = 
                { 
                Position={ x=readFloatNeg1 CV.EyePositionRightX
                           y=readFloatNeg1 CV.EyePositionRightY
                           z=readFloatNeg1 CV.EyePositionRightZ }
                GazePoint = pxToMm (readInt32Neg1 CV.GazePointRightX)(readInt32Neg1 CV.GazePointRightY)
                Valid=(getColumn CV.ValidityRight = "1")
                PupilDiameter=readFloatNeg1 CV.PupilDiameterRight
                }

            if i = 255759 then 
                printfn "line %d" i 
            let eyes = 
                { 
                    Left = left
                    Right = right
                    GazePointPixel = { i=readInt32Neg1 CV.GazePointX  
                                       j=readInt32Neg1 CV.GazePointY }
                    MovementType = lastStateEyeMovement
                    GazeDuration = 0.0
                    Event = (match getColumn CV.Event with 
                                | "pro" -> EyeEvent.ProSaccade 
                                | "anti" -> EyeEvent.AntiSaccade |x -> EyeEvent.Unknown x)
                } 

            { timeTarget= timeTarget; eyes= eyes; comment= lastStateTargetType }
            )
        |> Seq.toArray

    let mediaArr = Array.init media.Count (fun x -> "" )
    media
    |> Map.iter( fun k v -> mediaArr.[v] <- k )
 
    let moodArr =
        timeComments
        |> List.sortBy( fun (x,_) -> x )
        |> List.toArray

    printfn ""
    (data, countInvalids, mediaArr, patientName, moodArr)

let private loadTargets (uri:string) =
    try
        let tsvFile = CsvFile.Load( uri )
        let isPx = 
            match tsvFile.Headers with
            | Some x -> 
                x 
                |> Array.contains "PositionXPx"
            | None ->
                failwith (sprintf "No header found in EyeTargets.conf: %s" uri)

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
                        printfn "%A Exception: %A, col: %A str: %A" i e.Message s (x.GetColumn s)
                        0.0f

                let readUInt64 (s:string) = System.UInt64.Parse (x.GetColumn s)

                let prefix = x.GetColumn "Prefix"

                let pPx =
                    if isPx then 
                        {   i = int (readUInt64 "PositionXPx")
                            j = int (readUInt64 "PositionYPx") }
                    else
                        mmToPx (readFloat "PositionXMm") (readFloat "PositionYMm")
            
                let pMm = 
                    if isPx then 
                        pxToMm pPx.i pPx.j
                    else
                        {   x= readFloat "PositionXMm"
                            y= readFloat "PositionYMm"
                            z= 0.0f }
       
                {   IDName    = prefix
                    FullName  = prefix
                    Position  = pPx
                    PositionF = pMm
                    Diameter  = uint32 (readUInt64 "Diameter") }
            )
            |> Seq.toArray

        data   
    
    with 
    | :? System.IO.FileNotFoundException -> 
        System.Windows.Forms.MessageBox.Show( 
            sprintf "EyeTargets.conf missing! Please place at: %s " uri ) |> ignore
        Array.empty

let private loadTargetMedia (uri:string) (targets:Target[]): Dictionary<string, (string * int)> =
    
    let mediaToTarget = new Dictionary<string, (string * int)>()
    
    let loadedTargetNames =
        targets
        |> Array.map( fun x -> x.IDName )

    try
        let tsvFile = CsvFile.Load( uri )
        tsvFile.Rows
        |> Seq.iteri( fun i x -> 
            let readInt32 (s:string) = System.Int32.Parse (x.GetColumn s)

            let mediaName = clearMediaName (x.GetColumn "MediaName")
            let targetName = x.GetColumn "TargetName"

            let nr = 
                loadedTargetNames 
                |> Array.tryFindIndex (fun x -> targetName.ToLower().Contains(x.ToLower()))

            let targetNr = 
                match nr with
                    | Some tNr -> tNr 
                    | None -> 
                        printfn "Warning: targetName of Media.conf not found in EyeTarget.conf"
                        loadedTargetNames |> Array.iteri( fun i x -> printfn "%d: '%s'" i x)
                        printfn " -> targetName '%s' not found" targetName
                        -1
            
            mediaToTarget.Add( mediaName, (targetName, targetNr))
            )
        mediaToTarget
        
    with 
    | :? System.IO.FileNotFoundException -> 
        System.Windows.Forms.MessageBox.Show( 
            sprintf "Media.conf missing! Please place at: %s " uri ) |> ignore
        mediaToTarget
        

// -- helper function to create a session record
let private makeSession  
    (path:string) 
    (dataArr) 
    (targetDefs:Target[]) 
    (invalids) (media:string[]) 
    (name:string) 
    (timeComment: (float*string)[]) =
    let targets =
        if media.Length > 0 then
            media 
            |> Array.map( fun x-> 
                if x = "" then
                    None
                else
                    let nID, nFull = getTargetNamesLowerCase x
    
                    let found =
                        targetDefs
                        |> Array.tryFind( fun x -> nID.Contains (x.IDName.ToLower()) )

                    match found with
                    | Some x -> 
                        Some { 
                            IDName   = nID
                            FullName = nFull
                            Position = x.Position
                            PositionF= x.PositionF
                            Diameter = x.Diameter }
                    | None -> 
                        Some { 
                            IDName   = "Undefined"
                            FullName = "Undefined-"+nFull
                            Position = {i=0; j=0}
                            PositionF= {x=0.0f;y=0.0f;z=0.0f}
                            Diameter=10u }
            )
        else
            targetDefs
            |> Array.map( Some )
    
    let errors =
        try 
            let filePath = System.IO.Path.Combine( path, (sprintf "%s.ignore" name) )
            let tsvFile = CsvFile.Load( filePath ) 

            let sTargets =
                tsvFile.Rows
                |> Seq.map( fun x -> System.Int32.Parse (x.GetColumn( getCol CV.TargetNr)) )
        
            let sError =
                tsvFile.Rows
                |> Seq.map( fun x -> System.Int32.Parse (x.GetColumn( getCol CV.ErrorType)) )
        
            (Seq.zip sTargets sError) |> Map.ofSeq
        
        with
        | error ->
            printfn "WARN: No ignore file found on disk! %s" error.Message
            Map.empty
    
    let config =
        try 
            let filePath = System.IO.Path.Combine( path, (sprintf "%s.conf" name) )
            let readLines = System.IO.File.ReadLines( filePath )


            let mutable splitThreshold = 0.3

            readLines 
            |> Seq.iter( fun x ->
                let toks = x.Split('=')

                if toks.Length = 2 then
                    match toks.[0].Trim() with
                    | "SplitThreshold" -> splitThreshold <- System.Double.Parse( toks.[1].Trim() )
                    | _ -> printfn "Unknown config" )
            { splitThreshold = splitThreshold }
        with
        | error ->
            printfn "WARN: No ignore file found on disk! %s" error.Message
            { splitThreshold = 0.3 }

    let targetChanges = getTargetChanges dataArr

    let usedCorrections =
        try 
            let filePath = System.IO.Path.Combine( path, (sprintf "%s.corr" name) )
            let tsvFile = CsvFile.Load( filePath ) 

            let sReactionsStart =
                tsvFile.Rows 
                |> Seq.map( readFloatColumn CV.ReactionTimeStart )
                |> Seq.map( float )
        
            let sReactionsStop =
                tsvFile.Rows 
                |> Seq.map( readFloatColumn CV.ReactionTimeStop )
                |> Seq.map( float )

            let sReactions =
                sReactionsStop
                |> Seq.zip sReactionsStart
                |> Seq.map( fun x -> { x = fst x; y = snd x } )

            let sTargets =
                tsvFile.Rows
                |> Seq.map( fun x -> System.Int32.Parse( x.GetColumn (getCol CV.TargetNr) ) )

            { reactionTime = (Seq.zip sTargets sReactions) |> Map.ofSeq }
        with
        | error ->
            printfn "WARN: No correction file found on disk! %s" error.Message
            UserCorrectionData.Zero

    let targetChangedValid = getValidTargetChanges targetChanges targets dataArr
    let targetLabels = getTargetLabels targetChangedValid timeComment dataArr

    {   Name = name
        Path = path
        InvalidsNr = invalids
        EyeData = dataArr
        Targets = { All = targets
                    FirstIdx = findFirstTargetIdx dataArr targets
                    LastIdx = findLastTargetIdx dataArr targets
                    Changes = targetChanges
                    ChangesValid = targetChangedValid
                    Labels = targetLabels }
        BlinkData = Array.empty
        Errors = errors
        UserCorrections = usedCorrections
        TimeComment = timeComment 
        DataConfig = config }

// -- open an experiment from a file into a session
let getSession (uri:string) (targetUri:string) (mediaUri:string) (* statesUri *) =
    if GlobalCfg.InputData.ColsNamesCSV.Count = 0  then failwith "No config loaded"
        
    let cleanedUri = cleanHashCommentsFromASCIIFile( uri )

    let tsvFile = CsvFile.Load( cleanedUri )

    let listEmpty (lst:list<string>) = 
        if lst.Length = 1 then 
            if lst.Head = "" then 
                true
            else
                false
        else
            lst.IsEmpty

    let noControlLabels = 
        listEmpty GlobalCfg.InputData.StateExperiment && listEmpty GlobalCfg.InputData.StateTarget
    
    let targetProto = loadTargets targetUri   // overwritten name (!)
    let mediaAsTarget = loadTargetMedia filePathMedia targetProto

    let data, inv, media, _, moods = 
        if noControlLabels then 
            getFileAsArrayMediaChange tsvFile mediaAsTarget
        else
            getFileAsArrayTimedLabels tsvFile   

    let name = System.IO.Path.GetFileName uri

    let session = makeSession (System.IO.Path.GetDirectoryName uri) data targetProto inv media name moods
    
    let evalData = 
        [0 .. (session.Targets.ChangesValid.Length-1)]
        |> Seq.map( fun x -> evalTargetEvents session x )
        |> Seq.concat
        
    let bData =
        evalData 
        |> blinkIntervalsAllData
        |> Seq.toArray

    { session with BlinkData = bData }

     // UiState.state <- Some (UiState.setBlinkData bData s)


let saveSession (session:Session) (fileName:string) =
    let rec loop i max lst=
        if i <> max then 
            let vTarget = session.Targets.ChangesValid
            let evalData     = evalTargetEvents session i
            let intervalData = 
                baseIntervalsFromTarget evalData session.DataConfig session.Targets.ChangesValid.[i]
                              
            let tStart = (Seq.head evalData).Time.TimeStamp 
            let tEnd   = (Seq.last evalData).Time.TimeStamp

            let iLst =
                intervalData 
                |> List.map( fun x -> 
                    sprintf "%f, %f, %.0f, %.0f, %.0f, %s, %.5f, %.5f, %.5f, %.5f, %.5f, %.5f" 
                        tStart tEnd x.tStart.x x.tEnd.x (x.tEnd.x-x.tStart.x) 
                        (eyeEventToString x.tType) x.reg.k x.reg.d x.xErrorStat.mean 
                        x.xErrorStat.stdDev x.xErrorDistStat.mean x.xErrorDistStat.stdDev )
                |> List.rev

            loop (i+1) max (lst@iLst)
        else
            lst
        
    let lines = loop 0 (session.Targets.ChangesValid.Length) List.Empty  

    use csvFile = System.IO.File.CreateText( fileName )
    csvFile.WriteLine( "tStart, tEnd, iStart, iEnd, iDuration, iType, xK, xD, xErrMean, xErrStdDev, xErrDistMean, xErrDistStdDev" )

    lines
    |> Seq.iter( csvFile.WriteLine )
