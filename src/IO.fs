module IO

open OxyPlot
open System
open System.IO
open System.Windows.Forms
open OxyPlot

// blinky includes
open FSharp.Data
open System.Globalization
open OfficeOpenXml
open Configs
open ETData

open Plot

// Todo: use a record state object
let mutable openPath = GlobalCfg.ProgramState.OpenPath
let mutable savePath = GlobalCfg.ProgramState.SavePath
let mutable stateSkipError = GlobalCfg.ProgramState.StateSkipError
let mutable stateTargetNr = GlobalCfg.ProgramState.StateTargetNr
let mutable guiTargetFilter = GlobalCfg.ProgramState.GuiTargetFilter
let mutable guiSplitThreshold = -1.0
let mutable plotConfig = GlobalCfg.ProgramState.PlotConfig

let saveIniFiles (state:UiState.T) =
        let boolToStr x =
            if x then "true" else "false"

        use iniFile = File.CreateText( filePathIni )       
        iniFile.WriteLine( "OpenPath = " + openPath )
        iniFile.WriteLine( "SavePath = " + savePath )
        iniFile.WriteLine( "SkipErrors = " + boolToStr state.SkipErrors )
        iniFile.WriteLine( sprintf "TargetNr = %d" state.TargetNr )
        iniFile.WriteLine( "TargetFilter = " + guiTargetFilter)

        state.PlotConfig 
        |> List.iter( fun s -> iniFile.WriteLine( sprintf "Plot%s = true" s ) ) 

let private showSaveFileDialog (title) (filename) (fileExtensions) =
    use sfd = new SaveFileDialog()
    sfd.Title <- title
    sfd.InitialDirectory <- savePath
    sfd.RestoreDirectory <- true
    sfd.FileName <- filename
    sfd.Filter <- String.Join("|", fileExtensions |> Seq.map (fun (d, e, _) -> sprintf "%s|*.%s" d e))
    sfd.FilterIndex <- 1
    if sfd.ShowDialog() = DialogResult.OK then
        savePath <- Path.GetDirectoryName sfd.FileName
        try
            let (_, _, f) =
                fileExtensions
                |> List.item (sfd.FilterIndex - 1)
            Some (sfd.FileName, f sfd.FileName)
        with
        | error ->
            MessageBox.Show( "Error saving file on disk! " + error.Message ) |> ignore
            None
    else
        None


let public showOpenFileDialog (title) (filename) (fileExtensions) =
    let ofd = new OpenFileDialog()
    ofd.Title <- title
    ofd.InitialDirectory <- Path.GetDirectoryName openPath
    ofd.RestoreDirectory <- true
    ofd.FileName <- filename
    ofd.Filter <- String.Join("|", fileExtensions |> Seq.map (fun (d, e, _) -> sprintf "%s|*.%s" d e))
    ofd.FilterIndex <- 1
    if ofd.ShowDialog() = DialogResult.OK then
        openPath <- ofd.FileName
        try
            let (_, _, f) =
                fileExtensions
                |> List.item (ofd.FilterIndex - 1)
            Some (ofd.FileName, f ofd.FileName)
        with
        | error ->
            MessageBox.Show( "Error opening file on disk! " + error.Message ) |> ignore
            failwith error.Message
            None
    else
        None

let public showOpenMultiFileDialog (title) (filename) (fileExtensions) =
    let ofd = new OpenFileDialog()
    ofd.Title <- title
    ofd.InitialDirectory <- Path.GetDirectoryName openPath
    ofd.RestoreDirectory <- true
    ofd.FileName <- filename
    ofd.Filter <- String.Join("|", fileExtensions |> Seq.map (fun (d, e, _) -> sprintf "%s|*.%s" d e))
    ofd.FilterIndex <- 1
    ofd.Multiselect <- true
    if ofd.ShowDialog() = DialogResult.OK then
        openPath <- ofd.FileName
        try
            let (_, _, f) =
                fileExtensions
                |> List.item (ofd.FilterIndex - 1)
            Some (ofd.FileNames, f ofd.FileNames)
        with
        | error ->
            MessageBox.Show( "Error opening file on disk! " + error.Message ) |> ignore
            failwith error.Message
            None
    else
        None

let openSession filePath =
    printfn "open session!"
    let timer = System.Diagnostics.Stopwatch.StartNew()
 
    // -- read all data from file in data records and from target definition file
    let experiment = Csv.getSession filePath filePathEyeTargets

    // -- printf some read related stats    
    printfn "read in: %A [ms]" timer.Elapsed.TotalMilliseconds
    //EyeTracking.printSessionOverView experiment

    guiSplitThreshold <- experiment.DataConfig.splitThreshold

    printfn "split in %d/%d targets" 
        (Seq.length experiment.Targets.ChangesValid)
        (Seq.length experiment.Targets.All)
    
    printfn "Time Comments: %d" (experiment.TimeComment.Length)
    printfn "Valid Changes: %d" (experiment.Targets.ChangesValid.Length)

    printfn "loaded eye data %d" experiment.EyeData.Length 

    experiment

let openLastSession () =
    if File.Exists openPath then
        let newExperiment = openSession openPath
        Some newExperiment
    else
        None

// -- open file dialog to load a session file
let sessionFromFileDialog () =
    showOpenFileDialog "Save Session" "" [
        ("Tobii Eyetracker Data File", "tsv", fun filename ->
            openSession filename
        )
    ]
    |> Option.map snd

// -- store the actual figure as a svg, pdf, or png file
let storeModelAsFile (model:PlotModel) =
    showSaveFileDialog "Save Picture" "" [
        ("png file (*.png)", "png", fun filename ->
            use stream = File.Create filename
            let exporter = new OxyPlot.WindowsForms.PngExporter( Width = int(model.Width), Height = int(model.Height) )
            exporter.Export( model, stream ) 
        )
        ("svg file (*.svg)", "svg", fun filename ->
            use stream = File.Create filename
            let exporter = new OxyPlot.WindowsForms.SvgExporter ( Width = model.Width, Height = model.Height )
            exporter.Export( model, stream ) 
        )
        ("pdf file (*.pdf)", "pdf", fun filename ->
            use stream = File.Create filename
            let exporter = new OxyPlot.PdfExporter( Width = model.Width, Height = model.Height )
            exporter.Export( model, stream )
        )
    ]
    |> ignore



// -- store session as html report
let sessionToHTML (model:PlotModel) (s:ETData.Session) =
    showSaveFileDialog "Save HTML" (s.Name + ".html") [
        ("html file (*.html)", "html", fun filename ->
            let rec loop i max lst=
                if i <> max then 
                    let vTargets = s.Targets.ChangesValid
                    let evalData     = ETReaction.evalTargetEvents s i
                    let intervalData = ETReaction.baseIntervalsFromTarget evalData s.DataConfig s.Targets.ChangesValid.[i]
                    let blinkData = ETReaction.blinkIntervalsFromTarget evalData s.Targets.ChangesValid.[i]
                    let target       = snd vTargets.[i]
                    let targetStart    = s.EyeData.[fst vTargets.[i]].timeTarget.TimeStamp
                    let targetAnalysisData = ETReaction.analyseIntervalsForTarget intervalData (ETReaction.getErrorNr i (s.Errors)) i target.FullName targetStart

                    let targetLabel = 
                        if s.Targets.Labels.IsSome then 
                            let _, txt, _ = s.Targets.Labels.Value.[i]
                            txt
                        else "-"

                    let gazePlotDat = 
                        {
                            targetNr = fst vTargets.[i]
                            target = snd vTargets.[i]
                            targetLabel = targetLabel
                            currNr= i 
                            currSize= vTargets.Length
                            isError= (ETReaction.getErrorNr i (s.Errors))
                            data= evalData
                            xZoom= None
                            saccs= []
                            saccsData= []
                            config= []
                            timeComments= [||]
                            intervals= intervalData
                            targetStart= targetStart
                            blinkIntervals= blinkData
                            userCorr = s.UserCorrections
                            analysis = targetAnalysisData
                        }

                    Plot.updateGazeModel model gazePlotDat

                    
                    let fileName = sprintf "%s_%03d.svg" s.Name (i+1)
                   
                    use sw = File.CreateText( Path.Combine( Path.GetDirectoryName(filename), fileName ) )
                    let exporter = new SvgExporter( Width=model.Width, Height=model.Height )
                    exporter.Export( model, sw.BaseStream )
                
                    loop (i+1) max (lst@[fileName])
                else
                    lst
        
            let svgFiles = loop 0 (s.Targets.ChangesValid.Length) List<string>.Empty  
        
            let HtmlPrefix =
                "<html>
                <head></head>
                <body>
                <center>
                "
    
            let HtmlPostfix =
                "</center>
                </body>
                </html>
                "

            let htmlTxt = 
                HtmlPrefix +
                "<h1>" + s.Name + "</h1>\n" +
                (svgFiles
                |> List.indexed
                |> List.fold( fun acc (i, e) -> 
                    (acc + "<img src=\"" + e + "\" width=\"50%\">" + if i % 2 = 0 then "<br><br>\n" else ""  ) ) "" )
                + HtmlPostfix
        
            File.WriteAllText(filename, htmlTxt) //Path.Combine( initDirectory, s.Value.Name + ".html" ) )
        )
    ]
    |> ignore

// hmmmmm 
// http://www.c-sharpcorner.com/blogs/create-excel-file-with-formulas-in-c-sharp1
let sessionToCsv (s:ETData.Session) =
    showSaveFileDialog "Save as CSV" (s.Name + ".csv") [
        ("csv file", "csv", fun filename ->
            Csv.saveSession s filename
        )
    ]
    |> ignore

let saveSessionErrors (s:ETData.Session) =
    let fileName = sprintf "%s.ignore" s.Name
    use ignFile = File.CreateText( Path.Combine( (Path.GetDirectoryName openPath), fileName) )
    ignFile.WriteLine("TargetNr, ErrorType")
    s.Errors
    |> Seq.iter( fun x -> ignFile.WriteLine( sprintf "%d, %d" x.Key x.Value ) )

let saveSessionCorrections (s:ETData.Session) =
    let fileName = sprintf "%s.corr" s.Name
    use corrFile = File.CreateText( Path.Combine( (Path.GetDirectoryName openPath), fileName) )
    corrFile.WriteLine("TargetNr, ReactionTimeStart, ReactionTimeStop")
    s.UserCorrections.reactionTime
    |> Map.iter( fun nr rtime -> 
        printfn "Storing Correction: %d, %f, %f" nr rtime.x rtime.y
        corrFile.WriteLine( sprintf "%d, %f, %f" nr rtime.x rtime.y ) )

let saveSessionConfig(s:ETData.Session) =
   let fileName = sprintf "%s.conf" s.Name
   use ignFile = File.CreateText( Path.Combine( (Path.GetDirectoryName openPath), fileName) )
   ignFile.WriteLine( sprintf "SplitThreshold = %f" s.DataConfig.splitThreshold )
    
let saveExcelSheet (filterTargets) (s:ETData.Session) =
    showSaveFileDialog "Save Excel Sheet" (s.Name + ".xlsx") [
        ("xlsx file (*.xlsx)", "xlsx", fun filename ->
            //let filename = Path.Combine( openPath, sprintf "%s.xlsx" s.Name )
            Excel.saveSession s filterTargets filename
        )
    ]
    |> ignore


let saveValidBlinksTsv  
    (filename) 
    (hershy:RichOutData[])
    (targets:BTarget[]) =
    
    if File.Exists( filename ) then File.Delete( filename )

    let lines =
        targets
        |> Array.mapi( fun i x ->
            sprintf "%d\t%d\t%f\t%f\t%f\t%s\t%s\t%s\t%d\t%d\t%d\t%d\t%d\t%d"
                (int x.RecordingTimeStamp)
                (int x.TimeStamp)
                x.PupilDia.Left
                x.PupilDia.Right
                x.PupilDia.Avg
                (if x.ValidLeft then "Valid" else "Invalid")
                (if x.ValidRight then "Valid" else "Invalid")
                (if x.ValidBoth then "Valid" else "Invalid")
                hershy.[i].HershRawLeft
                hershy.[i].HershLeft
                hershy.[i].HershRawRight
                hershy.[i].HershRight
                hershy.[i].HershRawBoth
                hershy.[i].HershBoth
            )

    let linesWithHeader = Array.concat [| 
        [|"Recording timestamp\tComputer timestamp\tPupil diameter left\tPupil diameter right\tPupil diameter avg\tValidity left\tValidity right\tValidity both\tRaw Blink left\tHershBlink left\tRaw Blink right\tHershBlink right\tRaw Blink both\tHershBlink avg" |]
        lines |]

    File.WriteAllLines( filename, linesWithHeader)
    ()
