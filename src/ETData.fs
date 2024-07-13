module ETData

open Configs

type Config =
    {
        splitThreshold : float
    }

let pxToMm (px) (py) = 
//    let xk =  3.74205273f
//    let xd = -0.752033961f
//    let yk =  3.77777463f
//    let yd = -7.566204361f
    let xk = GlobalCfg.ExpSetup.Xk
    let xd = GlobalCfg.ExpSetup.Xd
    let yk = GlobalCfg.ExpSetup.Yk
    let yd = GlobalCfg.ExpSetup.Yd
    {
        x = (float32 px - xd) / xk
        y = (float32 py - yd) / yk
        z = 0.0f
    }

let mmToPx (mx) (my) = 
//    let xk =  3.74205273f
//    let xd = -0.752033961f
//    let yk =  3.77777463f
//    let yd = -7.566204361f
    let xk = GlobalCfg.ExpSetup.Xk
    let xd = GlobalCfg.ExpSetup.Xd
    let yk = GlobalCfg.ExpSetup.Yk
    let yd = GlobalCfg.ExpSetup.Yd
    {
        i = int (round (mx * xk + xd)) 
        j = int (round (my * yk + yd)) 
    }

type BlinkRange =
    {
        Start : float
        End   : float
    }
    static member Limits() = { x=100.0; y=500.0 }

type EyeEvent =
    | Fixation
    | ProSaccade
    | AntiSaccade
    | BadData
    | Blink
    | Unknown of string

let eyeEventToString e =
    match e with
    | Fixation    -> "Fixation"
    | ProSaccade  -> "ProSaccade"
    | AntiSaccade -> "AntiSaccade"
    | BadData     -> "BadData"
    | Blink       -> "Blink"
    | Unknown x   -> "Unknown: " + x

// -- data of an eye
type Eye =
    {
    Position : vec3
    GazePoint : vec3
    Valid : bool
    PupilDiameter:float32
    }
    static member Zero = {Position= vec3.Zero; GazePoint= vec3.Zero; Valid= false; PupilDiameter= 0.0f}

// -- data of a target
type Target =
    {
    IDName    : string
    FullName  : string
    Position  : ivec2
    PositionF : vec3
    Diameter  : uint32
    }
    static member Zero = {IDName="" ;FullName=""; Position=ivec2.Zero; PositionF=vec3.Zero; Diameter=0u}
    static member createBad name = {IDName=name; FullName=name; Position=ivec2.Zero; PositionF=vec3.Zero; Diameter=0u}


// -- data of one time-step in the measurement
type EyesData =
    {
    Left : Eye
    Right : Eye
    GazePointPixel : ivec2
    MovementType : string
    GazeDuration : float
    Event : EyeEvent
    }
    static member Zero = {Left= Eye.Zero; Right= Eye.Zero; GazePointPixel= ivec2.Zero; MovementType= ""; GazeDuration= 0.0; Event= (EyeEvent.Unknown "empty") }

type TimeAndTarget =
    {
        TimeStamp : float
        MediaNr : int
    }
    static member Zero = {TimeStamp= 0.0; MediaNr=0}

type EyesSnapshot =
    {
        timeTarget : TimeAndTarget
        eyes       : EyesData
        comment    : string
    }

type UserCorrectionData = 
    {
        reactionTime : Map<int,vec2>
    }
    static member Zero = { reactionTime = Map.empty<int,vec2> }

type Targets =
    {
        All : (Target option)[]
        FirstIdx : int
        LastIdx : int
        Changes : int list
        ChangesValid : (int*Target)[]
        Labels : (int*string*int)[] option
    }

module UserCorrectionData =
    let updateReactionTime (update) (data) =
        {data with reactionTime = update data.reactionTime}

type BIntervalData =
    {
        time   : Range<float>
        iType  : EyeEvent
        idxRangeRaw : Range<int>
        idxRHersh : Range<int>
//            idxRHershR : Range<int>
//            idxRHershA : Range<int>
    }
    static member Zero = {
        time = Range<float>.Zero
        iType  = EyeEvent.BadData
        idxRangeRaw = Range<int>.Zero
        idxRHersh = Range<int>.Zero
        }
    member x.toStr () =
        sprintf "(%f - %f), duration: %f, idxRangeRaw: %d-%d" x.time.Start x.time.End (x.time.End-x.time.Start) (x.idxRangeRaw.Start) (x.idxRangeRaw.End)

// -- final high level type representing one experiment
type Session =
    {
    Name : string                        // name of the session
    Path : string                        // file path to measurement file 
    InvalidsNr : int                     // invalid lines in measurement file
    EyeData : EyesSnapshot[]             // read lines of the measurment as organized data structure
    Targets : Targets                    // targets used in the experiment
    BlinkData : BIntervalData[]          // intervals of the executed blinks
    Errors : Map<int, int>               // target classification by error types
    UserCorrections : UserCorrectionData // user correction of reaction times
    TimeComment : (float*string)[]       // timed labels
    DataConfig : Config                  // data configuration
    }

module Session =
    let updateErrors (update) (session) =
        {session with Errors = update session.Errors}

    let updateUserCorrections (update) (session) =
        {session with UserCorrections = update session.UserCorrections}

// -- data from evaluation per timestep
type EvaluationData =
    {
    Time : TimeAndTarget
    Speed : float
    AngularDirection : float
    XError : float
    YError : float
    GazePointMean   : vec3
    EyePositionMean : vec3
    PupilSizeMean : float
    Valid : bool
    }
    static member Zero = 
        {
            Time = TimeAndTarget.Zero
            Speed = 0.0
            AngularDirection= 0.0
            XError= 0.0
            YError= 0.0
            GazePointMean= vec3.Zero
            EyePositionMean= vec3.Zero
            PupilSizeMean= 0.0
            Valid= false
        }

type Stat =
    {
        mean   : float
        stdDev : float
        min    : float
        max    : float
        size   : int
    }
    static member Zero = {mean = 0.0; stdDev = 0.0; min= 0.0; max= 0.0; size= 0}

type LinearRegression =
    {
        k : float
        d : float
    }

type IntervalData =
    {
    tStart : vec2
    tEnd   : vec2
    tType  : EyeEvent
    xErrorDistStat : Stat
    xErrorStat : Stat
    speed : Stat
    reg : Regression
    target : (int*Target)
    }
    static member Zero = {
        tStart = vec2.Zero
        tEnd   = vec2.Zero
        tType  = EyeEvent.BadData
        xErrorDistStat = Stat.Zero
        xErrorStat = Stat.Zero
        speed = Stat.Zero
        reg = Regression.Zero
        target = (0, Target.Zero) }

type EventAnalysisData =
    {
        target : (int*Target)
        targetNr : int
        reactionTime : float
        timeToFirstFixationTarget : float
        timeToFirstFixation : float
        firstFixationXError : float
        gainFirst : float
        gainBest : float
        nrOfCorrectionSaccades : int
        speed : Stat
        error : int
        timeStamp: float
    }
    static member Zero = {target= (0,Target.Zero); targetNr= 0; reactionTime= 0.0; timeToFirstFixationTarget= 0.0; timeToFirstFixation= 0.0; firstFixationXError= 0.0;  gainFirst=0.0; gainBest=0.0; nrOfCorrectionSaccades=0; speed= Stat.Zero; error=0; timeStamp =0.0}
    static member createBad targetNr targetName =
        {target= (0,Target.createBad targetName); targetNr= targetNr; reactionTime= 0.0; timeToFirstFixationTarget= 0.0; timeToFirstFixation= 0.0;firstFixationXError= 0.0; gainFirst=0.0; gainBest=0.0; nrOfCorrectionSaccades=0; speed= Stat.Zero; error=10;  timeStamp =0.0}


type PupilDiameter =
    {
        Left : float32
        Right : float32
        Avg : float32
    }
    static member Zero = { Left = 0.0f; Right = 0.0f; Avg = 0.0f}

type BTarget =
    {
        RecordingTimeStamp : float
        TimeStamp : float
        ValidBoth : bool
        ValidLeft : bool
        ValidRight: bool
        PupilDia : PupilDiameter 
    }
    static member Zero = {RecordingTimeStamp=0.0 ;TimeStamp=0.0; ValidBoth=false; ValidLeft=false; ValidRight=false; PupilDia = PupilDiameter.Zero}



// define valid time length for a blink.
// larger than 100ms lesser than 500ms
let BlinkLimits : Range<float> = 
    {
        Start= 100.0
        End= 500.0
    }
    //static member Limits() = { x=100.0; y=500.0 }

type RichOutData = 
    {
        HershRawLeft : int
        HershLeft : int
        HershRawRight : int
        HershRight : int
        HershRawBoth : int
        HershBoth : int
    }
