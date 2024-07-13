module ControlTheorie

open ETData
open System.Data
open Accord.Statistics
open Accord.Math
open Accord.Statistics.Models.Regression


type FittingParameters = 
    {
        A : float
        B : float
        C : float
        D : float 
    }
    member x.asArray() = [| x.A; x.B; x.C; x.D |]
    member x.asArray3() = [| x.A; x.B; x.C |]

    static member fromArray (a:float[]) =
        if a.Length <> 4 && a.Length <> 3 then
            failwith "Incompatible size in Fitting Params!" 
        {   A = a.[0]
            B = a.[1]
            C = a.[2]
            D = if a.Length = 4 then a.[3] else 0.0 }  

type SaccadeEval = 
    {
        Translate  : float
        MirrorY    : bool
        Abcd       : FittingParameters 
        Fitted     : vec2[]
        AxisCrds   : vec2[]
        AxisAngles : vec2[]
        FullAngles : float[]
    }
    static member Zero =
        {
            Translate = 0.0
            MirrorY = false
            Abcd = FittingParameters.fromArray [|0.0;0.0;0.0;0.0|]
            Fitted = [||]
            AxisCrds= [||]
            AxisAngles= [||]
            FullAngles= [||]
        }

let getSaccades (intervals: IntervalData list) (analysis : EventAnalysisData list) =
    
    //printfn "# Intervals %d" intervals.Length

    let mutable saccs   = List.empty
    let mutable saccade = List.empty

    intervals
    |> List.skipWhile( fun x -> x.tType = Fixation )
    |> List.iter( fun x ->
        if x.tType = Fixation then
            if not saccade.IsEmpty then
                if x.tStart.x - saccade.Head.tStart.x > 40.0 then
                    saccs <- saccs @ [saccade]
            
            saccade <- List.empty   
        else
            saccade <- saccade @ [x] )

    //printfn "Saccades: # %d" saccs.Length

    saccs
    |> List.map( fun x -> 
        { 
            x= x.Head.tStart.x 
            y= (x |> List.last).tEnd.x 
        } )
        
let getData (data:seq<EvaluationData>) (minMax:vec2) = 
    data
    |> Seq.filter( fun x -> 
        float x.Time.TimeStamp > minMax.x && float x.Time.TimeStamp < minMax.y )
    |> Seq.toArray
 

// y'' + a y' + b y - c x = 0
// subsitituion:
// u' = v
// v' = - a v - b u + c x
// finite differences:
// u_i+1 = u_i + h v_i
// v_i+1 = v_i - h ( a v_i + b u_i - c x_i )
let signalFunction h u0 v0 (w:float[]) (xP:float[]) =
    let rec step u v x i =
        if x >= xP.[0] || i > 500 then
            //printfn "did steps: %d -> %.2f" i u
            u
        else
            let vs = v - h * ( w.[0] * v + w.[1] * u - w.[2] * x )
            let us = u + h * v
            step us vs (x+h) (i+1)
    
    step u0 v0 0.0 0 

let signalGradient (w:float[]) (x:float[]) (r:float[]) =
    r.[0] <- w.[0]
    r.[1] <- w.[1]
    r.[2] <- w.[2]


let transferFunction (w:float[]) (x:float[]) =
    ( w.[0] * x.[0] + w.[1] ) / ( x.[0]**2.0 + w.[2] * x.[0] + w.[3] )

let gradientFunction (w:float[]) (x:float[]) (r:float[]) =
    let x = x.[0]
    let D1 = (x * (w.[2] + x) + w.[3])
    let D2 = (( x * (w.[2] + x) + w.[3])**2.0)
    let A  = (w.[0] * x + w.[1])
    r.[0] <-    x/D1
    r.[1] <-   1./D1
    r.[2] <- -A*x/D2
    r.[3] <-   -A/D2

let interpolateAt (dat:vec2[]) (x:float[]) =
    x
    |> Array.map( fun x ->
        match ( dat |> Array.tryFindIndex( fun y -> y.x >= x ) ) with
        | None -> 
            //failwith "re-sample index wrong?"
            dat.[dat.Length-1]                  // copy last value at the end
        | Some ii -> 
            if ii = 0 then
                dat.[0]
            else
                let y0 = dat.[ii-1].y           // linear interpolation
                let y1 = dat.[ii].y
                let x0 = dat.[ii-1].x
                let x1 = dat.[ii].x
                { 
                    x= x; 
                    y= y0 + (x - x0) * (y1-y0) / (x1-x0) 
                } )

let reSample (h:float) (dat:vec2[]) = 
    Array.init 
        (int(dat.[dat.Length-1].x / h) + 1)     // new length of resampled 
        (fun i -> (float i) * h )               // init uniform x values
    |> (interpolateAt dat)

let fitFunction (data:EvaluationData[]) = 

    let translate = - float data.[0].Time.TimeStamp

    let lastVal  = data.[data.Length-1].XError
    let firstVal = data.[0].XError

    let mirror   = 
        if  lastVal > firstVal then 
            true 
        else 
            false

    let normData =
        data
        |> Array.map( fun x -> 
            { 
                x = float x.Time.TimeStamp + translate     // shift to origin
                y = 
                    if mirror then 
                        -x.XError + lastVal 
                    else 
                        x.XError - lastVal  // always have a decreasing function
            } )

    let H = 3.0

    let resampled = 
        normData
        |> reSample H                                   // resample for constant stepsize

    printfn "shift %.2f %.2f" translate -lastVal
    resampled
    |> Array.iter( fun x -> printfn "%.2f %.2f" x.x x.y )

    let samples = 
        Array2D.init resampled.Length 2 
            (fun i j -> 
                if j = 0 then 
                    resampled.[i].x 
                else 
                    resampled.[i].y)
(*
    let startParams = 
        [| 0.249; 9.4e-06; 0.1634; 0.01193 |]
        |> FittingParameters.fromArray
*)
    let u0 = resampled.[0].y
    let v0 = 
        if resampled.Length > 1 then
            (resampled.[1].y - resampled.[0].y) / H
        else
            1.0

    let nls = 
        Fitting.NonlinearLeastSquares(
            NumberOfParameters = 3,
            StartValues = [|0.01; 0.01; 0.01|], 
//            StartValues = [| 225.3; 1.85e-04; 1.869e-04 |],
//            StartValues = startParams.asArray3(),
//            Function = RegressionFunction( transferFunction ),
//            Gradient = RegressionGradientFunction( gradientFunction ),
            Function = RegressionFunction( signalFunction (H/3.) u0 v0  ),
            Gradient = RegressionGradientFunction( signalGradient ),
            Algorithm = Optimization.LevenbergMarquardt(
                MaxIterations = 100,
                Tolerance = 0.0 ) )

    let inputs  = samples.GetColumn(0).ToJagged()
    let outputs = samples.GetColumn(1) 

    try 
        let regression = nls.Learn( inputs, outputs )

        Some {   
            Translate = translate
            MirrorY   = mirror
            Abcd = 
                regression.Coefficients
                |> FittingParameters.fromArray
            Fitted      =
                let reg = 
                    regression.Transform( inputs )  
                    |> Array.mapi( fun i r -> 
                        { 
                            x= resampled.[i].x // - translate
                            y= r
                        } )

                (normData |> Array.map(fun x -> x.x) )
                |> interpolateAt reg 
                |> Array.map( fun x -> 
                    {
                        x= x.x - translate
                        y=  if mirror then -x.y - lastVal else x.y + lastVal 
                    })
            AxisCrds  = [||]
            AxisAngles= [||]
            FullAngles= [||]
        }
    with
    | error ->
        printfn "WARN: No nonlinear regression was computable: %s" error.Message
        None

            

let computeAxisCrds (s:Session) (e:EvaluationData seq) (sac) =
    
    let ev = 
        e
        |> Seq.filter( fun x -> 
            let ftime = float x.Time.TimeStamp
            ftime >= sac.Fitted.[0].x && ftime <= sac.Fitted.[sac.Fitted.Length-1].x ) 
        |> Seq.toArray

    let tPos = s.Targets.All.[ev.[0].Time.MediaNr].Value.PositionF
    let sPos = ev.[0].GazePointMean

    let lenToTar = vec3.norm (tPos - sPos)
    let toTar    = vec3.unit (tPos - sPos)

    let axisCrds, axisAngles, fullAngles = 
        ev
        |> Array.map( fun x -> 
//            printfn "Target: %f %f\tGaze: %f %f" tPos.x tPos.y x.GazePointMean.x x.GazePointMean.y 
            
            let toGaze    = x.GazePointMean - sPos
            let lenInAxis = vec3.innerProduct toGaze toTar 
            let atAxis    = sPos + toTar * lenInAxis
            let lenFromAxis  = vec3.norm (x.GazePointMean - atAxis)

            let eyeDirGaze = 
                { x= x.GazePointMean.x; y= x.GazePointMean.y; z=0.0f } - 
                { x= x.EyePositionMean.x; y= x.EyePositionMean.y; z= x.EyePositionMean.z }

            let eyeDirAt = 
                { x= atAxis.x; y= atAxis.y; z= 0.0f } - 
                { x= x.EyePositionMean.x; y= x.EyePositionMean.y; z= x.EyePositionMean.z }

            let targetDir = 
                { x= tPos.x; y= tPos.y; z= 0.0f } - 
                { x= x.EyePositionMean.x; y= x.EyePositionMean.y; z= x.EyePositionMean.z }

            // sign might make no sense for full angle
            let fullAngle = (vec3.angleBetween eyeDirGaze targetDir) * (float (sign (lenToTar - lenInAxis)))

            let axisAngle = 
                {
                    vec2.x= (vec3.angleBetween eyeDirAt targetDir) * (float (sign (lenToTar - lenInAxis)))
                    y     = (vec3.angleBetween eyeDirAt eyeDirGaze) * (float (sign (x.GazePointMean.y - atAxis.y)))
                }

//            printfn "  -> %f %f" (lenToTar - lenInAxis) lenFromAxis
//            printfn "  o> %f, (%f, %f)" fullAngle axisAngle.x axisAngle.y


            ( { x= float (lenToTar - lenInAxis); y= float lenFromAxis }, axisAngle, fullAngle )
            )
        |> Array.unzip3
    
    { sac with AxisCrds = axisCrds; AxisAngles= axisAngles; FullAngles = fullAngles }
