//[<RequireQualifiedAccess>]

// -- data reading and evaluation code
module ETReaction // 590

open ETData

let getErrorNr (targetNr:int) (errors:Map<int,int>) =
    let res = errors.TryFind targetNr
    if res.IsSome then
        res.Value
    else
        0

let isDesiredTarget (filterTargets) (name:string) =
    if Seq.isEmpty filterTargets
    then true
    else
        filterTargets
        |> Seq.exists name.Contains

let findFirstTargetIdx (arr:EyesSnapshot[]) (targets:Option<Target>[]) =
    arr
    |> Array.tryFindIndex (fun x -> 
        if x.timeTarget.MediaNr < 0 then false else
        Option.isSome targets.[ x.timeTarget.MediaNr ] )
    |> function
        | None -> -1
        | Some i -> i

let findLastTargetIdx (arr:EyesSnapshot[]) (targets:Option<Target>[]) =
    arr
    |> Array.tryFindIndexBack (fun x -> 
        if x.timeTarget.MediaNr < 0 then false else
        Option.isSome targets.[ x.timeTarget.MediaNr ])
    |> function
        | None -> -1
        | Some i -> i
        
let getTargetChanges (arr:EyesSnapshot[]) =
    let changes =
        arr
        |> Seq.indexed
        |> Seq.pairwise
        |> Seq.choose (function
            | ((ia, a), (ib, b))
                when a.timeTarget.MediaNr <> b.timeTarget.MediaNr
                -> Some ib
            | _ -> None)
        |> Seq.toList
    0 :: changes

let getValidTargetChanges (targetChanges) (targets:(Target option)[]) (data:EyesSnapshot[]) =
    targetChanges
    |> Seq.choose( fun elem -> 
        if data.[elem].timeTarget.MediaNr < 0 then None else
        match targets.[data.[elem].timeTarget.MediaNr] with 
        | Some x -> Some (elem, x)
        | None -> None )
    |> Seq.toArray

let getTargetLabels (targetChangesValid:(int*Target)[]) (timeComments:(float*string)[]) (data:EyesSnapshot[]) =
    let flaggedLabels = 
        timeComments
        |> Array.filter( fun (_,s) -> s.[0]='+' )
    
    // check if the numer is consistent, assign target labels by number
    if flaggedLabels.Length = targetChangesValid.Length then
        Some (
            flaggedLabels
            |> Array.mapi( fun i (_,s) -> (fst targetChangesValid.[i], s, i) ) )
    else
        // as the number is no correct, check if there are any labels at all
        if flaggedLabels.Length = 0 then
            None
        else
            // try to assign by label by timestamp (+-1100 ms), set none if nothing is found
            // map targets to maybe found labels
            Some (
                targetChangesValid
                |> Array.map( fun (i,t) -> 
                    let targetStamp = data.[i].timeTarget.TimeStamp 
                    let candidates = 
                        flaggedLabels
                        |> Array.mapi( fun i (tc,s) -> ( abs(targetStamp - tc), s, i ) )
                        |> Array.filter( fun (tc,s, i) -> if tc < 1100.0 then true else false )
                        |> Array.sort
                    if candidates.Length > 0 then
                        let _, txt, labelIdx = candidates.[0]
                        (i, txt, labelIdx)
                    else
                        (i, "none", -1)
                    ) )

let getTargetNames (x:string) =
    printfn "getTargetNames %A" x

    let nameToDot   (y:string) = y.Substring(0,y.IndexOf('.'))
    let nameToSpace (y:string) = y.Substring(0,y.IndexOf(' '))

    let nFull = if x.Contains( "." ) then nameToDot x else x
    if nFull.Contains( " " ) then
        (nameToSpace nFull, nFull)
    else
        (nFull, nFull)
     
 
let rec findPastValidSnapshot (i:int) (data:EyesSnapshot[]) =
    if i < 0 then
        None
    else
        if data.[i].eyes.Left.Valid then
            Some data.[i]
        else
            findPastValidSnapshot (i-1) data

// -- analyze the data of given session and target interval
let evalTargetEvents (s:Session) (idx) =
    // -- helper functions
    let norm a = 
        sqrt (float (a.i * a.i + a.j * a.j))

    let sub a b =
        { i=a.i-b.i; j=a.j-b.j }

    let angle a =
        (atan2 (float a.j) (float a.i)) / System.Math.PI * 180.0

    // -- compute direction as vector and index // !!!!

    let lastIdx = 
        if idx < s.Targets.ChangesValid.Length - 1 then 
            (fst s.Targets.ChangesValid.[idx+1]-1)
        else
            s.Targets.LastIdx
        
    let directions =
        [  fst s.Targets.ChangesValid.[idx] ..  lastIdx ]
        |> List.map ( fun x -> 
            if s.EyeData.[x].eyes.Left.Valid <> true  || s.EyeData.[x].eyes.Right.Valid <> true then
                (x, None)
            else
                if x = 0 then
                    (x, Some( sub s.EyeData.[x+1].eyes.GazePointPixel s.EyeData.[x].eyes.GazePointPixel ) )
                else
                    (x, Some( sub s.EyeData.[x].eyes.GazePointPixel s.EyeData.[x-1].eyes.GazePointPixel ) ) )

//        |> List.filter( fun x -> x.IsSome )
//        |> List.map( fun x -> x.Value )

    // -- evaluate based on direction vector and index
    directions
    |> Seq.map( fun x_option -> 
        
        let i, dir_opt = x_option

        let dati = s.EyeData.[i]
        let timi = dati.timeTarget

        if dir_opt.IsNone then
            {
                Time  = timi
                Speed = 0.0
                AngularDirection = 0.0
                XError = 0.0
                YError = 0.0
                GazePointMean   = vec3.Zero
                EyePositionMean = vec3.Zero
                PupilSizeMean = 0.0
                Valid = false
            }
        else
            try
                let d = dir_opt.Value
                let dati = s.EyeData.[i]
                let eyes = dati.eyes
                let timi = dati.timeTarget
    (*            
                if (eyes.GazePointLeft.x = 0.0f && eyes.GazePointRight.x <> 0.0f) || (eyes.GazePointRight.x = 0.0f && eyes.GazePointLeft.x <> 0.0f) then 
                    printfn  "Unmatching GazePoints  %d %A %A" i eyes.GazePointLeft.x eyes.GazePointRight.x

                if (eyes.Left.Position3.x = 0.0f && eyes.Right.Position3.x <> 0.0f) || (eyes.Right.Position3.x = 0.0f && eyes.Left.Position3.x <> 0.0f) then 
                    printfn  "Unmatching Eye Positions  %d %A %A" i eyes.Left.Position3.x eyes.Right.Position3.x
    *)
                let GazePointMean = vec3.meanPreferred eyes.Left.GazePoint eyes.Right.GazePoint
                let EyePositionMean = vec3.meanPreferred eyes.Left.Position eyes.Right.Position
                let PupilSizeMean = meanIfPositive eyes.Left.PupilDiameter eyes.Right.PupilDiameter 

                let EyeDirX = 
                    { x = GazePointMean.x; y=0.0f; z=0.0f } - 
                    { x = EyePositionMean.x; y = 0.0f; z = EyePositionMean.z }
                let EyeDirY = 
                    { x = 0.0f; y=GazePointMean.y; z=0.0f } - 
                    { x = 0.0f; y=EyePositionMean.y; z = EyePositionMean.z }

                let TargetDirX = 
                    { x = s.Targets.All.[timi.MediaNr].Value.PositionF.x; y=0.0f; z=0.0f } - 
                    { x = EyePositionMean.x; y = 0.0f; z = EyePositionMean.z }
                let TargetDirY = 
                    { x=0.0f; y = s.Targets.All.[timi.MediaNr].Value.PositionF.y; z=0.0f } - 
                    { x = 0.0f; y=EyePositionMean.y; z = EyePositionMean.z }

                let hasNoTarget = s.Targets.All.[timi.MediaNr].IsNone

                let xError = 
                    if hasNoTarget then
                        0.0
                    else
                        (vec3.angleBetween EyeDirX TargetDirX) * (sgn (eyes.GazePointPixel.i - s.Targets.All.[timi.MediaNr].Value.Position.i) )

                let yError = 
                    if hasNoTarget then
                        0.0
                    else
                        (vec3.angleBetween EyeDirY TargetDirY) * (sgn (eyes.GazePointPixel.j - s.Targets.All.[timi.MediaNr].Value.Position.j) )

                {
                    Time = timi

                    // -- speed in degree per s
                    Speed = 
                        if i = 0 then
                               0.0
                               //let GazePointMeanNext = (s.Data.[i+1].GazePointLeft + s.Data.[i+1].GazePointRight)*0.5f
                               //let EyePositionMeanNext = (s.Data.[i+1].Left.Position3 + s.Data.[i+1].Right.Position3)*0.5f 
                       
                               //let time = float (s.Data.[i+1].Timestamp - s.Data.[i].Timestamp)
                               //(float(angleVec3 (GazePointMeanNext-EyePositionMeanNext) (GazePointMean-EyePositionMean))/time)*1000.0
                        else
                            match findPastValidSnapshot (i-1) s.EyeData with
                            | Some snapShot -> 
                                let GazePointMeanPrev = vec3.meanPreferred snapShot.eyes.Left.GazePoint snapShot.eyes.Right.GazePoint
                                let EyePositionMeanPrev = vec3.meanPreferred snapShot.eyes.Left.Position snapShot.eyes.Right.Position
                                let time = float (timi.TimeStamp - snapShot.timeTarget.TimeStamp)
                                (float(vec3.angleBetween (GazePointMean-EyePositionMean) (GazePointMeanPrev-EyePositionMeanPrev))/time)*1000.0
                            | None -> 0.0

                    AngularDirection = angle d
            
                    XError = xError
                    YError = yError
                    GazePointMean   = GazePointMean
                    EyePositionMean = EyePositionMean
                    PupilSizeMean = (float)PupilSizeMean
                    Valid = if hasNoTarget then false else true
                }
            with
            | e -> 
                printfn "%A/%A" i s.EyeData.Length
                printfn "Error: %A" e.Message

                let d = dir_opt.Value

                printfn "%A %A" i d 
                        
                printfn "%A" s.EyeData.[i].timeTarget.TimeStamp
            
                let nd = norm d
                let b1 = s.EyeData.[i].timeTarget.TimeStamp
                let b2 = s.EyeData.[i-1].timeTarget.TimeStamp
                let ad = angle d
                printfn "i=%A %A %A %A %A" i nd b1 b2 ad

                printfn "MedianNr: %A " s.EyeData.[i].timeTarget.MediaNr
                //printfn "MedianNr: %A " media.[s.Data.[i].MediaNr]

                (*
                (
                    //printfn "->%A" s.Data.[i].Timestamp
                    s.Data.[i].Timestamp, 
                    (if i = 0 then 
                        (norm d) / float (s.Data.[i+1].Timestamp - s.Data.[i].Timestamp)
                    else

                        (norm d) / float (b1 - b2)), 
                    (angle d),
                    (s.Data.[i].GazePoint.i - s.Targets.[s.Data.[i].MediaNr].Value.Position.i ),
                    (s.Data.[i].GazePoint.j - s.Targets.[s.Data.[i].MediaNr].Value.Position.j ) )
                    *)
                {
                    Time  = {TimeStamp= float i; MediaNr= 0}
                    Speed = float i
                    AngularDirection = float i
                    XError = float i
                    YError = float i
                    GazePointMean   = vec3.Zero
                    EyePositionMean = vec3.Zero
                    PupilSizeMean = 0.0
                    Valid = false
                }
            )
    //|> List.toArray

let computeEyeEvent k =
    if abs k < 0.006 then
        EyeEvent.Fixation
    else if k > 0.006 then
        EyeEvent.ProSaccade
    else if k < 0.006 then
        EyeEvent.AntiSaccade
    else
        EyeEvent.Unknown "Other K"

let baseIntervalsFromTarget (data:seq<EvaluationData>) (conf:Config) (tar:(int*Target)) = 
    let errArr =
        data
        |> Seq.filter( fun x -> x.Valid )
        |> Seq.map( fun x -> ({x= float x.Time.TimeStamp; y= float x.XError}, x.Valid) )
        |> Seq.filter ( fun x -> not (System.Double.IsNaN (fst x).y) ) // filter nans, dont filter invalids)
        |> Seq.toArray

    let spdArr =
        data
        |> Seq.filter( fun x -> x.Valid )
        |> Seq.filter ( fun x ->  not (System.Double.IsNaN (float x.XError) ) ) // filter nans, dont filter invalids
        |> Seq.map( fun x -> x.Speed )
        |> Seq.toArray

    let mutable intervals = List.Empty
    let mutable history = RegressionHistory.Zero
    let mutable startIdx = 1

    let getDistErrStat (xyArr:vec2[]) sIdx eIdx hist =
        let subArr=
            [|sIdx .. eIdx|]
            |> Array.map( fun i -> xyArr.[i] )
        
        let m= 
            (subArr
            |> Array.sumBy( fun xy ->  abs (Regression.dist xy hist) ) ) / (float subArr.Length)

        let sSq= 
            (subArr
            |> Array.sumBy( fun xy ->  ((Regression.dist xy hist) - m ) ** 2.0 ) ) / (float subArr.Length - 1.0)
        let mi  = (subArr |> Array.minBy( fun xy ->  abs (Regression.dist xy hist) ) )
        let ma  = (subArr |> Array.maxBy( fun xy ->  abs (Regression.dist xy hist) ) )

        { mean= m ; stdDev= sqrt sSq; min= abs (Regression.dist mi hist); max = abs (Regression.dist ma hist); size= subArr.Length  }

    let getErrStat (xyArr:vec2[]) sIdx eIdx =   
        let subArr=
            [|sIdx .. eIdx|]
            |> Array.map( fun i -> xyArr.[i].y )
        
        let m   = (subArr |> Array.sum ) / (float subArr.Length)
        let sSq = (subArr |> Array.sumBy( fun y -> (y - m ) ** 2.0 ) ) / (float subArr.Length - 1.0)
        let mi  = (subArr |> Array.min )
        let ma  = (subArr |> Array.max )

        { mean= m ; stdDev= sqrt sSq; min= mi; max = ma; size= subArr.Length }

    let getSpdStat (arr:float[]) sIdx eIdx =
        let subArr=
            [|sIdx .. eIdx|]
            |> Array.map( fun i -> arr.[i] )
        
        let m   = (subArr |> Array.sum ) / (float subArr.Length)
        let sSq = (subArr |> Array.sumBy( fun y -> (y - m ) ** 2.0 ) ) / (float subArr.Length - 1.0)
        let mi  = (subArr |> Array.min )
        let ma  = (subArr |> Array.max )

        { mean= m ; stdDev= sqrt sSq; min= mi; max = ma; size= subArr.Length }

    let lastIdx = errArr.Length-1

    let eArr = (errArr |> Array.unzip |> fst) // error values
    let vArr = (errArr |> Array.unzip |> snd) // validity values

    for i in 0 .. lastIdx do

        if i > 0 then
            if vArr.[i-1] = false && vArr.[i] then // split on invalid change
                let ms = eArr.[i-1].x - eArr.[startIdx-1].x
                let msRange = BlinkRange.Limits()
                intervals <- intervals @ 
                   [{ IntervalData.Zero with 
                        tStart= eArr.[startIdx-1]
                        tEnd= eArr.[i-1]
                        tType= if ms < msRange.x || ms > msRange.y then EyeEvent.BadData else EyeEvent.Blink
                        target=tar }] 
                history   <- RegressionHistory.Zero
                startIdx  <- i

            else if vArr.[i-1] && (vArr.[i] = false) then // split on invalid change
                let reg = Regression.toLinear history
                
                let errStat = getErrStat eArr (startIdx-1) (i-1)
                intervals <- intervals @ 
                   [{ tStart= eArr.[startIdx-1]
                      tEnd= eArr.[i-1]
                      tType= computeEyeEvent reg.k
                      xErrorStat= errStat
                      xErrorDistStat= getDistErrStat eArr (startIdx-1) (i-1) reg
                      speed= getSpdStat spdArr (startIdx-1) (i-1)
                      reg = reg
                      target=tar }] 
                history   <- RegressionHistory.Zero
                startIdx  <- i


        if history.count > 2 then
            let reg = Regression.toLinear history
            if abs (Regression.dist eArr.[i] reg) > conf.splitThreshold then // 0.3 then // split for interval
                let errStat = getErrStat eArr (startIdx-1) (i-1)
                let isBad = 
                    if (eArr.[i-1].x - eArr.[startIdx-1].x) / 10.0 > float errStat.size then 
                        true 
                    else 
                        false
                intervals <- intervals @ 
                    [{ tStart= eArr.[startIdx-1]
                       tEnd= eArr.[i-1]
                       tType= if isBad then EyeEvent.BadData else computeEyeEvent reg.k
                       xErrorStat= errStat
                       xErrorDistStat= getDistErrStat eArr (startIdx-1) (i-1) reg
                       speed= getSpdStat spdArr (startIdx-1) (i-1)
                       reg = reg
                       target=tar }]
                history   <- RegressionHistory.Zero
                startIdx  <- i

        history <- Regression.add eArr.[i] history

    // collect last interval
    if startIdx <> lastIdx && startIdx <= lastIdx then  
        let errStat = getErrStat eArr (startIdx-1) (lastIdx-1)
        let isBad = 
            if (eArr.[lastIdx-1].x - eArr.[startIdx-1].x) / 10.0 > float errStat.size then 
                true 
            else 
                false
        let reg = Regression.toLinear history
        intervals <- intervals @ 
            [{ tStart= eArr.[startIdx-1] 
               tEnd= eArr.[lastIdx-1]
               tType = if isBad then EyeEvent.BadData else computeEyeEvent reg.k
               xErrorStat= getErrStat eArr (startIdx-1) (lastIdx-1)
               xErrorDistStat= getDistErrStat eArr (startIdx-1) (lastIdx-1) reg
               speed= getSpdStat spdArr (startIdx-1) (lastIdx-1)
               reg= reg
               target=tar }]

//        if i > 3 then
//            printfn "T=%d k=%.2f d=%.2f r=%f | diff: %f | dist: %f" time.[i] history.reg.k history.reg.d history.reg.rSq (history.reg.rSq - 1.0) (dist errArr.[i] history)
(*                  
    printfn "Nr intervals: %d" intervals.Length

    intervals
    |> Seq.iter (fun x -> printfn "k=%f" x.Reg.k )
*)
    intervals


let blinkIntervalsFromTarget  (data:seq<EvaluationData>) (tar:(int*Target)) =

    let mutable intervalsBlink = List.Empty
    let mutable startIdx = 1

    let errArrB =
        data
        |> Seq.map( fun x -> ({x= float x.Time.TimeStamp; y= float x.XError}, x.Valid) )
        |> Seq.filter ( fun x -> not (System.Double.IsNaN (fst x).y) ) // filter nans, dont filter invalids)
        |> Seq.sortBy( fun x -> (fst x).x )
        |> Seq.toArray


    let eArrB = (errArrB |> Array.unzip |> fst) // error values
    let vArrB = (errArrB |> Array.unzip |> snd) // validity values
    let lastIdxB = errArrB.Length-1

    for i in 0 .. lastIdxB do

        if i > 0 then
            if  (vArrB.[i-1] = false && vArrB.[i]) ||    // split on invalid change 
                (vArrB.[i] = false && i = lastIdxB) then // or end of time slice
                let ms = eArrB.[i-1].x - eArrB.[startIdx-1].x
                let msRange = BlinkRange.Limits()
                intervalsBlink <- intervalsBlink @ 
                   [{ IntervalData.Zero with 
                        tStart= eArrB.[startIdx-1]
                        tEnd= eArrB.[i-1]
                        tType= if ms < msRange.x || ms > msRange.y then EyeEvent.BadData else EyeEvent.Blink
                        target=tar }] 
            else if vArrB.[i-1] = true && vArrB.[i] = false then
                startIdx  <- i

    intervalsBlink    


    
let analyseIntervalsForTarget (ints:IntervalData list) (errorNr:int) targetNr targetName targetStart =
 
    if not ints.IsEmpty then 
        // if (snd ints.Head.target).IDName.Contains("CueL") ||  (snd ints.Head.target).IDName.Contains("CueR")  then

        let fix = ints |> List.filter( fun x -> x.tType = EyeEvent.Fixation )

            
        let validData = 
            ints 
            |> List.where( fun x -> 
                x.tType = EyeEvent.BadData    
            )
            |> List.isEmpty


        if not fix.IsEmpty && fix.Head.tType <> EyeEvent.BadData then
                
            // -- reaction phase is the starting fixation interval  
            let reaction = fix.Head
            
            // -- find first fixcation on target
            let firstFixationTarget = 
                try
                    Some (fix |> List.find( fun x -> (abs x.xErrorStat.mean) < 3.0 ))
                with
                | _ -> None

            let firstFixation =
                if fix.Length > 1 then
                    Some (fix |> List.item 1)
                else
                    None


            //-- evaluate nr of saccades
            let mutable nrSacc = 0 
            let mutable mean = reaction.xErrorStat.mean
            fix
            |> List.iter( fun x -> 
                if abs x.xErrorStat.mean < abs mean then  
                    nrSacc <- nrSacc + 1
                    mean <- x.xErrorStat.mean )

            //-- evaluate speed to first fixation (in saccade)
            let saccade =
                if firstFixationTarget.IsSome then
                    Some 
                        (ints 
                        |> List.filter( fun x -> 
                            (x.tStart > ints.Head.tStart) && 
                            (x.tStart < firstFixationTarget.Value.tStart) && 
                            (x.tType <> EyeEvent.Fixation) ) )
                else
                    None

            let mutable spdStat = Stat.Zero
            if saccade.IsSome then
                saccade.Value
                |> List.iteri( fun i x ->
                        if i = 0 then
                            spdStat <- x.speed 
                        else
                            spdStat <- 
                                { 
                                    mean   = ( spdStat.mean * (float spdStat.size) + x.speed.mean * (float x.speed.size) ) /  ( float( spdStat.size + x.speed.size ) )
                                    stdDev = ( spdStat.stdDev * (float spdStat.size) + x.speed.stdDev * (float x.speed.size) ) /  ( float( spdStat.size + x.speed.size ) )
                                    min = min spdStat.min x.speed.min
                                    max = max spdStat.max x.speed.max
                                    size = spdStat.size+x.speed.size
                                }
                    )
            
            [{
                target = ints.Head.target
                targetNr = targetNr
                reactionTime = reaction.tEnd.x - targetStart //reaction.tStart.x
                timeToFirstFixationTarget = if firstFixationTarget.IsSome then firstFixationTarget.Value.tStart.x - reaction.tStart.x else 0.0
                timeToFirstFixation =  if firstFixation.IsSome then firstFixation.Value.tStart.x - reaction.tStart.x else 0.0
                firstFixationXError =  if firstFixation.IsSome then firstFixation.Value.xErrorStat.mean else 0.0
                gainFirst = if firstFixationTarget.IsSome then firstFixationTarget.Value.xErrorStat.mean else 0.0
                gainBest = mean
                nrOfCorrectionSaccades = nrSacc - 1
                speed   = if saccade.IsSome then spdStat else Stat.Zero
                error   = 
                    if errorNr = 0 then
                        if not validData then 9 else 0
                    else
                        errorNr
                timeStamp = ints.Head.tStart.x
            }]
        else
            [{
                target = ints.Head.target
                targetNr = targetNr
                reactionTime = 0.0
                timeToFirstFixationTarget = 0.0
                timeToFirstFixation = 0.0
                firstFixationXError = 0.0
                gainFirst = 0.0
                gainBest = 0.0
                nrOfCorrectionSaccades =0
                speed   = Stat.Zero
                error   = 
                    if errorNr = 0 then
                        if not validData then 9 else 0
                    else
                        errorNr
                timeStamp = 0.0
            }]
    else
        let nID, _ = getTargetNames targetName
        [ EventAnalysisData.createBad targetNr nID ]


let printSessionOverView s = 
    // -- print some session stats
    printfn "having entries: %A, %A have been invalid"  s.EyeData.Length s.InvalidsNr
    printfn "First target at: %d" s.Targets.FirstIdx
    printfn "Last  target at: %d" s.Targets.LastIdx
    //printXRandomSamples experiment 3

    // -- print list of valid targets
    printfn "Valid Targets: %d" s.Targets.ChangesValid.Length
    s.Targets.ChangesValid
    |> Array.iter( fun x -> 
        let i, t = x
        printfn "%d:\t%s\tPos=(%d,%d)\tDia=%d " i t.IDName t.Position.i t.Position.j t.Diameter )

// -- print some random data of an experiment
let printXRandomSamples s x =
    printfn "\nSome random samples:"
    let rnd = System.Random()
    for i = 1 to x do 
        let idx = rnd.Next( s.Targets.FirstIdx, s.Targets.LastIdx )
        printfn " <---- Random entry at [%A] \n%A" idx s.EyeData.[idx]
        printfn "       with media file: %A" s.Targets.All.[s.EyeData.[idx].timeTarget.MediaNr]

    
let blinkIntervalsAllData (data:seq<EvaluationData>) =

    let mutable intervalsBlink = List.Empty
    let mutable startIdx = 1

    let errArrB =
        data
        |> Seq.map( fun x -> (float x.Time.TimeStamp, x.Valid) )
//        |> Seq.filter ( fun x -> not (System.Double.IsNaN (fst x).y) ) // filter nans, dont filter invalids)
        |> Seq.sortBy( fun x -> fst x )
        |> Seq.toArray


    let eArrB = (errArrB |> Array.unzip |> fst) // error values
    let vArrB = (errArrB |> Array.unzip |> snd) // validity values
    let lastIdxB = errArrB.Length-1

    for i in 0 .. lastIdxB do

        if i > 0 then
            if  (vArrB.[i-1] = false && vArrB.[i]) ||    // split on invalid change 
                (vArrB.[i] = false && i = lastIdxB) then // or end of time slice
                let ms = eArrB.[i-1] - eArrB.[startIdx-1]
                let msRange = BlinkRange.Limits()
                intervalsBlink <- intervalsBlink @ 
                   [{ BIntervalData.Zero with 
                        time= { Start= eArrB.[startIdx-1]; End= eArrB.[i-1] }
                        iType= if ms < msRange.x || ms > msRange.y then EyeEvent.BadData else EyeEvent.Blink
                        idxRangeRaw= {Start= startIdx; End= i}
                        idxRHersh= Range<int>.Zero }] 
            else if vArrB.[i-1] = true && vArrB.[i] = false then
                startIdx  <- i

    intervalsBlink
