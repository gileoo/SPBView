
open System.Windows.Forms
open System.Drawing
open OxyPlot
open System
open IO
open DataPreps
open UpdateView

// v0.1.1 -- first hotfixes for media target splitting reading for  newer tobii device (adjust reading of changed outputs)
// v0.2.0 -- fixed for target and image name redirection in Media.conf, and fixed hanglich of user-const values in InputData.conf

// todo:
// - Check excel spreadsheet values meaning
// - negative gain? has to be relative to the motion
// - implement control theory function regression
// - Target filter not working on start (retype-enter-works)
// - add GUI for target configuration
// - diameter in mm?
// - configurable resolution
// - update todos

// -- short type name
type Sc = Shortcut

(*
// -- global state variables
let mutable state = 
    let bindNone (bind) = 
        function 
        | Some x -> Some x 
        | None -> bind ()

    IO.openLastSession ()
    |> bindNone IO.sessionFromFileDialog
    |> Option.map (UiState.create IO.plotConfig IO.stateSkipError IO.stateTargetNr)
*)

[<STAThread>]
[<EntryPoint>]
let main argv = 

    let mutable programInited = false

    // printfn "to be removed in hardcoded"
    // Blink.allBlinkHack( "C:/Users/Marsello/Joint Attention 5901 Recording_1 (2).tsv" )

    UiState.state <- 
        let bindNone (bind) = 
            function 
            | Some x -> Some x 
            | None -> bind ()

        IO.openLastSession ()
        |> bindNone IO.sessionFromFileDialog
        |> Option.map (UiState.create IO.plotConfig IO.stateSkipError IO.stateTargetNr) 

    // -- prepare view for gaze plots
    let gazePlotFigure  = new PlotModel()
    use gazePlotView =
        new WindowsForms.PlotView( 
            Size = Size( 1024, 512 ), 
            Dock = DockStyle.Fill )

    // -- prepare view for blink durations
    let blinkPlotFigure  = new PlotModel()
    use blinkPlotView =
        new WindowsForms.PlotView( 
            Size = Size( 1024, 48 ), 
            Dock = DockStyle.Fill )

    // -- prepare view for all targets (classification, navigation)
    let allTargetsPlotFigure  = new PlotModel()
    use allTargetsPlotView =
        new WindowsForms.PlotView(
            Size = Size( 1024, 32 ), 
            Dock = DockStyle.Fill )

    UpdateView.updateState (UiState.setPlotWidth 1024.0)

    use win =
        new Form(
            Text = "SPBView v0.1.0",
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable,
            ClientSize = gazePlotView.Size + Drawing.Size( 0, 100 ),
            ShowIcon = false )

    use aboutWin =
        new Form(
            Text = "About SPBView",
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable,
            ClientSize = Drawing.Size( 320, 170 ),
            ShowIcon = false )

    // -- state change functions
    let stateDo (action) =
        match UiState.state with
        | None -> ()
        | Some s -> action s

    let sessionDo (action) = stateDo (fun s -> action s.Session)

    use filterText = 
        let tmp = 
            new TextBox(
                Size = Size( 300, 30 ),
                Text = guiTargetFilter )
        tmp.TextChanged.Add( fun e -> guiTargetFilter <- tmp.Text)
        tmp

    filterText.TextChanged.Add(fun ev ->
        UpdateView.updateState (fun s ->
            {s with
                FilterTarget =
                    filterText.Text.Split(' ')
                    |> Seq.map (fun x -> x.Trim())
                    |> List.ofSeq
            }
        ) )

    use splitThresholdText = 
        let tmp = 
            new TextBox(
                Size = Size( 300, 30 ),
                Text = sprintf "%.3f" guiSplitThreshold )
        tmp.TextChanged.Add( fun e -> 
            guiSplitThreshold <- Double.Parse( tmp.Text ) )
        tmp

    splitThresholdText.TextChanged.Add(fun ev ->
        UpdateView.updateState (fun s ->
            {s with
                Session = 
                    {s.Session with 
                        DataConfig = {splitThreshold = guiSplitThreshold }}
            }
        ) )

 
    let addReactionTimeToCurrentTarget (pos) (state) =
        let speedSeries =
            gazePlotFigure.Series
            |> Seq.find (fun s -> s.Title = "Speed")
            :?> Series.StemSeries

        let minX = speedSeries.MinX
        let p = speedSeries.InverseTransform pos

        UiState.addReactionTimeToCurrentTarget minX p.X state
 
    // -- add key events, target navigation controls
    gazePlotFigure.KeyDown.Add (fun ev ->
        match ev.Key with
        | OxyKey.Backspace ->
            // -- recompute for previous target event and update plots
            match UiState.state with
            | Some s ->
                if s.SkipErrors then
                    UpdateView.updateState UiState.previousValidTarget
                else 
                    UpdateView.updateState UiState.previousTarget
            | _ -> ()
        | OxyKey.Space ->
            // -- recompute for next target event and update plots
            match UiState.state with
            | Some s ->
                if s.SkipErrors then
                    UpdateView.updateState UiState.nextValidTarget
                else 
                    UpdateView.updateState UiState.nextTarget
            | _ -> ()
        | OxyKey.Enter ->
            // -- reset to first target event and update plots
            UpdateView.updateState UiState.firstTarget

        (*
        | OxyKey.Tab -> 
            UpdateState.updateState UiState.toggleTimeWarp
        *)
        | _ ->  ()

        match UiState.state with
        | Some s ->
            UpdateView.updateGaze gazePlotFigure gazePlotView s
            Plot.updateBlinks blinkPlotFigure UiState.state.Value.Session.BlinkData UiState.state.Value.XAxisMinMax
            updateView blinkPlotView blinkPlotFigure

            updatePlotTargets allTargetsPlotFigure allTargetsPlotView gazePlotFigure gazePlotView blinkPlotFigure blinkPlotView UiState.state.Value
        | _ ->  ()
    )

    // -- add mouse events, correction of reaction time
    gazePlotFigure.MouseDown.Add (fun ev -> 
        if ev.IsControlDown then
            match ev.ChangedButton with
            | OxyMouseButton.Left ->
                UpdateView.updateState (fun s ->
                    s
                    |> addReactionTimeToCurrentTarget ev.Position
                    |> UiState.setClickPosMode true )
            | _ -> () 
    )

    
    gazePlotFigure.MouseUp.Add( fun ev ->
        match UiState.state with 
        | Some s -> 
            if not s.ClickPosMode then ()
            else
                UpdateView.updateState( fun s ->
                    s
                    |> addReactionTimeToCurrentTarget (ev.Position)
                    |> UiState.setClickPosMode false )
                UpdateView.stateDo (UpdateView.updateGaze gazePlotFigure gazePlotView)

        | None -> ()
    )
    

    // -- create windows
    use tableLayoutAbout =
        new TableLayoutPanel( 
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1
        )

    use textAbout = new  Label( Text = "\n\n SPBView assists in saccadic eye tracking data evaluation. \n\n (c) 2024, v 0.1.0\n\n Please visit the URL below for more information:", Dock = DockStyle.Fill, AutoSize = true )

    use linkAbout = new LinkLabel(Text=" https://github.com/gileoo/SPBView ", Dock = DockStyle.Fill, AutoSize = true)
    linkAbout.Click.Add( fun e -> 
        System.Diagnostics.Process.Start("https://github.com/gileoo/SPBView") |> ignore )

    use buttonAbout = new Button( Text = "Ok", TextAlign = ContentAlignment.MiddleCenter, Dock=DockStyle.Right )
    buttonAbout.Click.Add( fun e -> aboutWin.Hide() )

    tableLayoutAbout.Controls.Add (textAbout, 0, 0)
    tableLayoutAbout.Controls.Add (linkAbout, 0, 1)
    tableLayoutAbout.Controls.Add (buttonAbout, 0, 2)
    aboutWin.Controls.Add tableLayoutAbout

    win.Menu <-
        let updateGazeStateAndPlots (update) =  
            match UiState.state with 
            | Some _ -> 
                UpdateView.updateState update
                UpdateView.updateGaze gazePlotFigure gazePlotView UiState.state.Value
                Plot.updateBlinks blinkPlotFigure UiState.state.Value.Session.BlinkData UiState.state.Value.XAxisMinMax
                UpdateView.updateView blinkPlotView blinkPlotFigure
            | None -> ()

        let sub (name:string) (items:MenuItem[]) = new MenuItem(name, items)
        let action (name) (shortcut) (action) = new MenuItem(name, (fun s e -> action ()), shortcut)
        let update (name) (shortcut) (update) = new MenuItem(name, (fun s e -> updateGazeStateAndPlots update), shortcut)
        let option (name) (shortcut) (option) = 
            let tmp = new MenuItem(name, (fun s e -> ()), shortcut)
            tmp.Checked <- IO.stateSkipError
            tmp.Click.Add( 
                fun e -> 
                    tmp.Checked <- not tmp.Checked
                    UpdateView.updateState option)
            tmp

        let plotOpt (name) (shortcut) = 
            let tmp = new MenuItem(name, (fun s e -> ()), shortcut)
            tmp.Checked <- 
                match UiState.state with
                | Some s -> (s.PlotConfig |> List.contains name)
                | None -> false
            tmp.Click.Add( 
                fun e -> 
                    tmp.Checked <- not tmp.Checked
                    UpdateView.updateState (UiState.togglePlotConfig name) 
                    printf "PlotConfig:"
                    match UiState.state with
                    | Some s -> 
                        s.PlotConfig
                        |> List.iter( printfn "%s") 
                        UpdateView.stateDo (UpdateView.updateGaze gazePlotFigure gazePlotView)
                    | None -> () )
            tmp

        new MainMenu [|
            sub "File" [|
                action "Open" Sc.CtrlO (fun () -> 
                    //-- save user changes
                    sessionDo IO.saveSessionErrors
                    sessionDo IO.saveSessionCorrections
                    sessionDo IO.saveSessionConfig
                    //-- load new data
                    let skipVal = if UiState.state.IsSome then UiState.state.Value.SkipErrors else IO.stateSkipError

                    UiState.state <-
                        IO.sessionFromFileDialog ()
                        |> Option.map (UiState.create IO.plotConfig skipVal stateTargetNr)

                    UpdateView.updateState id
                )

                action "Save"         Sc.CtrlS (fun () -> IO.storeModelAsFile gazePlotFigure)
                action "SaveAll"      Sc.CtrlA (fun () -> sessionDo (IO.sessionToHTML gazePlotFigure))
                action "SaveStats"    Sc.CtrlT (fun () -> sessionDo IO.sessionToCsv)
                new MenuItem("-")
                action "Quit"         Sc.AltF4 (fun () -> win.Close())
            |]

            sub "Analysis" [|
                action "SaveAnalysis" Sc.CtrlN (fun () -> stateDo (fun s -> IO.saveExcelSheet s.FilterTarget s.Session))
                new MenuItem("-")
                action "SaveBlinkAnalysis" Sc.CtrlB (fun () -> ETBlink.allBlinkHack(Array.empty) )
                action "ShowBlinks" Sc.CtrlL (fun () -> 
                    let blinkFiles = ETBlink.selectAllBlinkFiles(Array.empty)
                    if blinkFiles.IsSome then 
                        let data = ETBlink.blinksToPlotData blinkFiles.Value
                        ETBlink.plotBlinkAnnotations gazePlotFigure data
                        gazePlotView.Model <- gazePlotFigure
                        gazePlotView.Show() 
                    else
                        ()
                    )
            |]

            sub "DataPreps" [|
                action "CleanBlinkFiles" Sc.CtrlShiftC (fun () ->
                    let blinkFiles = ETBlink.selectAllBlinkFiles(Array.empty)
                    if blinkFiles.IsSome then
                        blinkFiles.Value
                        |> Array.iter( ETBlink.cleanResultFiles )
                    )
                action "SplitBlinkFiles" Sc.CtrlShiftC (fun () ->
                    let blinkFiles = ETBlink.selectAllBlinkFiles(Array.empty)
                    if blinkFiles.IsSome then
                        blinkFiles.Value
                        |> Array.iter( ETBlink.splitResultFiles 10)
                    )
                action "AddFileNameAsFirstColumn" Sc.CtrlShiftN ( fun() ->
                    let ofd = new System.Windows.Forms.FolderBrowserDialog()
                    let res = ofd.ShowDialog()
                    if res = DialogResult.OK && not (System.String.IsNullOrWhiteSpace(ofd.SelectedPath)) then
                        DataPreps.addFileNameAsFirstColumn ofd.SelectedPath "\t" 100 
                    )
                action "FixTOBIIOutput" Sc.CtrlShiftT ( fun() ->
                    // TODO adjustable user input for subdirectories
                    let subdirs = 
                        [
                            "Anti Arousal"
                            "Anti Klassisch"
                            "Anti Scramble"
                            "Pro Arousal"
                            "Pro Klassisch"
                            "Pro Scramble"
                        ]

                    let ofd = new System.Windows.Forms.FolderBrowserDialog()
                    let res = ofd.ShowDialog()
                    if res = DialogResult.OK && not (System.String.IsNullOrWhiteSpace(ofd.SelectedPath)) then
                        DataPreps.fixEyeTOBIIOutput ofd.SelectedPath subdirs
                    )
            |]

            sub "Target" [|
                update "Next"          Sc.AltRightArrow (UiState.nextTarget)
                update "Previous"      Sc.AltLeftArrow  (UiState.previousTarget)
                update "NextValid"     Sc.AltUpArrow    (UiState.nextValidTarget)
                update "PreviousValid" Sc.AltDownArrow  (UiState.previousValidTarget)
                update "First"         Sc.AltDownArrow  (UiState.firstTarget)
                update "MarkAsNoError" Sc.Ctrl0         (UiState.markTarget 0)
                update "MarkAsError1"  Sc.Ctrl1         (UiState.markTarget 1)
                update "MarkAsError2"  Sc.Ctrl2         (UiState.markTarget 2)
                update "MarkAsError3"  Sc.Ctrl3         (UiState.markTarget 3)
                update "MarkAsError4"  Sc.Ctrl4         (UiState.markTarget 4)
                update "MarkAsError5"  Sc.Ctrl5         (UiState.markTarget 5)
                update "MarkAsError6"  Sc.Ctrl6         (UiState.markTarget 6)
                update "MarkAsError7"  Sc.Ctrl7         (UiState.markTarget 7)
                update "MarkAsError8"  Sc.Ctrl8         (UiState.markTarget 8)
                update "MarkAsBadData" Sc.CtrlB         (UiState.markTarget 9)
            |]

            sub "Options" [|
                option "SkipErrors"    Sc.CtrlE (UiState.toggleSkipError)
            |]

            sub "View" [|
                plotOpt "Speed" Sc.Alt0
                plotOpt "XError" Sc.Alt1
                plotOpt "YError" Sc.Alt2
                plotOpt "EError" Sc.Alt3
                plotOpt "Intervals" Sc.Alt4
                plotOpt "PupilSize" Sc.Alt5
                plotOpt "LRegression" Sc.Alt6
                plotOpt "NLRegression" Sc.Alt7
                plotOpt "AInError" Sc.Alt8
                plotOpt "AFromError" Sc.Alt9
                plotOpt "AFullError" Sc.Alt0
            |]

            sub "Help" [|
                action "User Manual"  Sc.CtrlShiftM (fun _ -> 
                    System.Diagnostics.Process.Start("https://github.com/gileoo/SPBView/blob/master/doc/Manual.md") |> ignore )
                action "About" Sc.CtrlShiftA (fun _ -> 
                    aboutWin.Show();
                    )
            |]
        |]

    use tableLayout =
        new TableLayoutPanel( 
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 4
        )

    tableLayout.Controls.Add (new Label( Text = "Filter", TextAlign = ContentAlignment.MiddleRight), 0, 0)
    tableLayout.Controls.Add (filterText, 1, 0)
    tableLayout.Controls.Add (new Label( Text = "SplitThrs", TextAlign = ContentAlignment.MiddleRight), 2, 0)
    tableLayout.Controls.Add (splitThresholdText, 3, 0)

    tableLayout.Controls.Add (blinkPlotView, 0, 1)
    tableLayout.SetColumnSpan(blinkPlotView, 4 )
    tableLayout.Controls.Add (allTargetsPlotView, 0, 2)
    tableLayout.SetColumnSpan(allTargetsPlotView, 4 )
    tableLayout.Controls.Add (gazePlotView, 0, 3)
    tableLayout.SetColumnSpan(gazePlotView, 4 )
    win.Controls.Add tableLayout

    // -- add menu with events and shortcuts

    win.Closing.Add( fun ev ->
        IO.saveIniFiles (UiState.state.Value)
        sessionDo IO.saveSessionErrors
        sessionDo IO.saveSessionCorrections
        sessionDo IO.saveSessionConfig
    )

    // printfn "main: updateState"

    UpdateView.stateDo (UpdateView.updateGaze gazePlotFigure gazePlotView)
    UpdateView.stateDo (UpdateView.updatePlotTargets allTargetsPlotFigure allTargetsPlotView gazePlotFigure gazePlotView blinkPlotFigure blinkPlotView)
    UpdateView.updateView allTargetsPlotView allTargetsPlotFigure
   
   
    Plot.updateBlinks blinkPlotFigure UiState.state.Value.Session.BlinkData UiState.state.Value.XAxisMinMax
    updateView blinkPlotView blinkPlotFigure
    

    allTargetsPlotFigure.MouseMove.Add( fun ev ->
        if ev.IsShiftDown then
            let mousePos = { x= ev.Position.X; y= ev.Position.Y }
            UpdateView.updateState (UiState.setMousePosition mousePos)
            UpdateView.updateState (UiState.setPlotWidth allTargetsPlotFigure.Width)

            UpdateView.stateDo (UpdateView.updatePlotTargets allTargetsPlotFigure allTargetsPlotView gazePlotFigure gazePlotView blinkPlotFigure blinkPlotView)
        
            UpdateView.updateView allTargetsPlotView allTargetsPlotFigure
    )


    programInited <- true

//    UpdateState.updateAllViews gazePlotView gazePlotFigure blinkPlotView blinkPlotFigure allTargetsPlotView allTargetsPlotFigure

    // -- open dialog
    win.ShowDialog() |> ignore  

    0
