module DataPreps

open System.IO
open System

open FSharp.Data

// check timestamp range and partial order
// check nr of tabs
// move invalid entries in separate file
// add event field at end
// if 1 tab, add all tabs before last -> event if in same column


let isFloatOrInt (s:string) =
    let ck1, _ = Double.TryParse( s )
    let ck2, _ = Int64.TryParse( s )
    ck1 || ck2

let fixEyeTrackingFile (basePath:string) (subdir:string) (file:string) =

    let path = Path.Combine(basePath, subdir)

    let ext  = "tsv"
    let tmp = file.Split( '.' )

    let file = tmp.[tmp.Length-2]
   
    let lines = 
        File.ReadLines( Path.Combine( path, file + "." + ext ) )
        |> Seq.toArray

    let mutable count = 0

    let mutable skipNext = 0

    let correctTabs1 =
        lines
        |> Array.splitAt( 17 )
        |> snd
        |> Array.filter( fun x -> x <> "" )

    let correctTabs =
        correctTabs1
        |> Array.mapi( fun i x ->
            if skipNext <> 0 then 
                skipNext <- skipNext - 1
                "" 
            else 
            let toks = x.Split('\t')
            let tabs = toks.Length - 1
            match tabs with
            | 1 -> 
                //printfn "![%d] tabs: %d - %s" i tabs x
                x + "\t\t\t\t\t\t\t\t\t\t\t\t"
            | 13 -> x
            | 14 ->
                let t1 = Double.TryParse( toks.[0] )
                let tEvent = Double.TryParse( toks.[13].Substring(1) )
            
                if fst t1 && fst tEvent then
                    if (snd t1) < (snd tEvent) then
                        let upTo13 = 
                            ("", fst (toks |> Array.splitAt 13) )
                            ||> Array.fold( fun acc el -> if acc <> "" then acc + "\t" + el else el )
                        upTo13 + "\t" + toks.[13].Substring(0, 1) + "\n" + sprintf "%.4f" (snd tEvent) + "\t" + toks.[14] + "\t\t\t\t\t\t\t\t\t\t\t\t"
                    else
                        ""
                else 
                    ""
            | 10 -> 
                
                let upTo8 = 
                    ("", fst (toks |> Array.splitAt 9) )
                    ||> Array.fold( fun acc el -> if acc <> "" then acc + "\t" + el else el )

                let timeNext, original8 = 
                    if toks.[9].Chars( 0 ) = '-' then 
                        (toks.[9].Substring( 2 ), toks.[9].Substring( 0, 2 ))
                    else
                        (toks.[9].Substring( 3 ), toks.[9].Substring( 0, 3 ))
                let event = toks.[10]

                if (isFloatOrInt event) = false then
                    skipNext <- 1

                    let corr = upTo8 + "\t" + original8 + correctTabs1.[i+1] + "\n" + timeNext + "\t" + event + "\t\t\t\t\t\t\t\t\t\t\t\t"
                    corr
                else
                    x

            | 8 -> 
                skipNext <- 1
                let upTo6 = 
                    ("", fst (toks |> Array.splitAt 7 ) )
                    ||> Array.fold( fun acc el -> if acc <> "" then acc + "\t" + el else el )

                let timeNext, original6 = 
                    (toks.[7].Substring( 1 ), toks.[7].Substring( 0, 1 ))
                let event = toks.[8]

                if (isFloatOrInt event) = false then
                    skipNext <- 1

                    let corr = upTo6 + "\t" + original6 + correctTabs1.[i+1] + "\n" + timeNext + "\t" + event + "\t\t\t\t\t\t\t\t\t\t\t\t"
                    corr
                else
                    x

            | _ -> 
                printfn "?[%d] tabs: %d - %s" i tabs x 
                count <- count + 1
                x )

    printfn "Invalid lines: %d" count
       
    let sorted = 
        correctTabs
        |> Array.filter( fun x -> x <> "" )
        |> Array.sortBy( fun x -> 
            let toks = x.Split( '\t' )
            let chk, time = Double.TryParse( toks.[0] )
            if not chk then 
                -1.0 
            else
                time )

    let outBasePath = basePath + "_c"

    try
        Directory.CreateDirectory( outBasePath ) |> ignore
        Directory.CreateDirectory( Path.Combine( outBasePath, subdir ) ) |> ignore
    with 
    | e -> printfn "error creating dir: %s" (e.ToString())

    let outPath = Path.Combine( outBasePath, subdir )

    printfn "Wanna write in %s" outPath

    File.WriteAllLines( Path.Combine( outPath, file + "_c." + ext), sorted )


    let linesAgain = 
        File.ReadLines( Path.Combine( outPath, file + "_c." + ext) )
        |> Seq.toArray

    use correct = new IO.StreamWriter( Path.Combine( outPath, file + "_tocorrect.log" )  )

    let mutable lastIdx = 0

    linesAgain
    |> Array.iteri( fun i x -> 
        let toks = x.Split('\t')
        let tabs = toks.Length - 1
        match tabs with
        | 13 -> ()
        | _ -> 
            if i - lastIdx > 3 then
                correct.WriteLine( sprintf "[%d] %s" i x  ) 
            lastIdx <- i )


let fixEyeTOBIIOutput (baseDir) (subDirs) =
    let pre = "subject-"
    let post = "_TOBII_output"

    //let path = "C:/2020/Projects/EyeTrackingPsy/ant_neu/anti klassisch"
    //let path = "C:/2020/Projects/EyeTrackingPsy/klassisch2"



    subDirs
    |> Seq.iter( fun x -> 
        let path = Path.Combine(baseDir, x)
        
        try
            IO.Directory.GetFiles( path )

            |> Array.map( fun s -> 
                let toks = s.Split('\\')
                toks.[toks.Length-1] )  
            |> Array.iter( fixEyeTrackingFile baseDir x )
            printfn "Done: %A !" path 
        with 
        | e -> printfn "error reading dir: %s --> skipping, msg: '%s;" path e.Message
        )

    //[1 .. 83]
    //|> List.map( fun x -> sprintf "%s%d%s" pre x post )
    //|> List.iter( fixFile path ) 

    printfn "Done!"


let fixEdaFile (filePath:string) =

    let tsvFile =  FSharp.Data.CsvFile.Load( filePath )
    let toks = filePath.Split([| '.'; '\\'; '/' |])
    let rtoks = toks |> Array.splitAt (toks.Length-2) |> fst

    let path = ("", rtoks) ||> Array.fold( fun acc el -> 
        if acc = "" then 
            el 
        else 
            acc + "/" + el
             //Path.Combine( acc, el )
             )

    let name = toks.[toks.Length-2]

    let outFilePath = Path.Combine( path, name + "_c.txt" ) 
    printfn "outFilePath: '%s'" outFilePath

    let dataTs, dataEda, dataHr =
        tsvFile.Rows
        |> Seq.mapi( fun _ x -> 
            let ts  = System.Int64.Parse( x.GetColumn "Timestamp" )
            let eda = System.Double.Parse( x.GetColumn "EDA" ) 
            let hr  = System.Int32.Parse( x.GetColumn "HR" )
            (ts, eda, hr)
            )
        |> Seq.toArray
        |> Array.unzip3

    
    let diffAvg =
        dataTs
        |> Array.mapi( fun i x -> 
            if i > 0 then
                float x - float dataTs.[i-1]
            else
                0.0 )
        |> Array.tail
        |> Array.average

    let min = dataTs |> Array.min
    let max = dataTs |> Array.max
    
    let maxHr = dataHr |> Array.max

    //printf ", start: %d, duration: %.2f [mins], avgdiff: %.1f [ms] / %.1f [Hz], avgEDA: %.2f, max hr: %d\n" min (float (max - min) / 1000.0 / 60.0) diffAvg (1000.0/diffAvg) (Array.average dataEda) maxHr
    
    
    let outLines =
        dataTs
        |> Array.mapi( fun i x -> sprintf "%.2f\t%f\t%d" (float x) dataEda.[i] dataHr.[i] )

    File.WriteAllLines( outFilePath, outLines )

    ()
            

let doEdaSession (baseDir) =
    IO.Directory.GetFiles( baseDir )
    |> Array.iteri( fun i s -> 
        //printfn "%d %s" i s 
        //if i = 2 then
            fixEdaFile s )


let splitTSVFileByUniqueName (fileName:string) (delimiter:char) =
    let preFix = Path.Combine(Path.GetDirectoryName( fileName ), "corr" )
//    let postFix1 = fileName.Substring( fileName.LastIndexOf("1_") )
//    let postFix = postFix1.Split( [|'.'|]).[0] +    
//        if postFix1.Contains("33") then "_33"
//        else if postFix1.Contains("66") then "_66"
//        else ""
    
//    let postFix = postFix1.Substring( 0, postFix1.LastIndexOf(".")  )

    // new GERT
    let postFix = Path.GetFileNameWithoutExtension( fileName )


    let lines = File.ReadAllLines (fileName)
    
    let mutable lefts = List.Empty

    let header = 
        lines
        |> Array.head

    let tail =
        lines 
        |> Array.tail
    
    let mutable firstTimeStamp = 0L

    let names = 
        tail
        |> Array.mapi(fun i x -> 
            let toks = x.Split( [| delimiter |] )
            if toks.Length = 0 then 
                None
            else
                if i = 0 then firstTimeStamp <- Int64.Parse( toks.[1] )
                Some toks.[0] 
            )
        |> Array.filter(fun x -> x.IsSome)
        |> Array.map(fun x -> x.Value)
        |> Array.distinct
        
    let results = List.Empty

    names
    |> Array.iter( fun n ->
        printf "."
        // filter all lines for one patient
        let newLines = 
            tail 
            |> Array.filter( fun x -> 
                let toks = x.Split( [| delimiter |] )
                if toks.Length <> 0 then
                    n = toks.[0]
                else
                    false )

        // collect start time stamp for the session
        let toks = (newLines |> Array.head).Split( [| delimiter |] )
        firstTimeStamp <- Int64.Parse( toks.[1] )

        newLines
        |> Array.iter( fun x -> 
            if x.Contains("Left") then
                let toks2 = x.Split( [| delimiter |] )
                let timeStamp = Int64.Parse( toks2.[1] )
                let react = timeStamp - firstTimeStamp
                printf "(-)" 
                lefts <- lefts @ [ ( n, postFix, firstTimeStamp, timeStamp, timeStamp - firstTimeStamp ) ]
             )

        let allLines =  Array.concat [ [| header |]; newLines ] 

        File.WriteAllLines( (sprintf "%s/%s_%d%s.tsv" preFix n firstTimeStamp postFix ), allLines ) )

    lefts



let doFixNeuroSession (baseDir) =

    let mutable lefts = list.Empty

    IO.Directory.GetFiles( baseDir )
    |> Array.iter( fun s -> 
        printfn "doing: %s" s
        lefts <- lefts @ splitTSVFileByUniqueName s '\t'
        printfn ""
        )

    let dataLines = 
        lefts
        |> List.sort
        |> List.map( fun (a,b,c,d,e) ->
            sprintf "%s\t%s\t%d\t%d\t%d" a b c d e)
        |> List.toArray
        
    let resLines = Array.concat [ [| "Name, Purpose, TimeStart, TimeStamp, TimeDiff" |]; dataLines ]
    File.WriteAllLines( (sprintf "%s/corr/AllLefts.tsv" baseDir), resLines )


let addFileNameAsFirstColumn  baseDir delim prefixLen =

    let files = IO.Directory.GetFiles( baseDir )
    
    let mutable outLines = list.Empty

    files 
    |> Array.iteri( fun i s -> 
        let lines = File.ReadAllLines(s)
        if i = 0 then outLines <- outLines @ [ sprintf "Name%s%s" delim lines.[0] ]
        
        let toks = s.Split( [| '.'; '/'; '\\' |] )
        let fName = toks.[toks.Length-2]

        let name = if (fName.Length >= prefixLen) then fName.Substring(0, prefixLen) else fName

        lines
        |> Array.tail
        |> Array.iter( fun x -> 
            if x.Trim().Length > 0 then
                outLines <- outLines @ [ sprintf "%s%s%s" name delim x ]
        )
    )

    File.WriteAllLines(Path.Combine(baseDir, "out.txt"), List.toArray outLines)


let twoRowsToOneRow filePath postFix1 postFix2 =
    let lines = File.ReadAllLines(filePath)
    let header = lines.[0].Split([| '\t'; ';' |])

    let mutable outLines = list.Empty

    let newHeader = 
        [
            (header |> Array.map( fun x -> x + postFix1 ))       
            (header |> Array.map( fun x -> x + postFix2 ))
        ] 
        |> Array.concat
        |> Array.reduce( fun x y -> x + "\t" + y )

    let linesAB =
        lines 
        |> Array.tail
        |> Array.partition( fun x -> x.Contains(postFix1) )

    outLines <- outLines @ [ newHeader ]

    linesAB 
    ||> Array.zip
    |> Array.iter( fun x ->
        outLines <- outLines @ [ fst x + "\t" + snd x ])

    File.WriteAllLines(Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + "AB.txt"), List.toArray outLines)
    
(*
// [<EntryPoint>]
let main argv =
    //addFileNameAsFirstColumn "C:/2023/Research/EDA Charisma Auswertung/CF" "\t" 100
    //addFileNameAsFirstColumn "C:/2023/Research/EDA Charisma Auswertung/CV" "\t" 100
    //addFileNameAsFirstColumn "C:/2023/Research/EDA Charisma Auswertung/NF" "\t" 100
    //addFileNameAsFirstColumn "C:/2023/Research/EDA Charisma Auswertung/NV" "\t" 100

    twoRowsToOneRow "C:/2023/Research/EDA Charisma Auswertung/CF/outCF.txt" "Baseline" "Event"
    twoRowsToOneRow "C:/2023/Research/EDA Charisma Auswertung/CV/outCV.txt" "Baseline" "Event"
    twoRowsToOneRow "C:/2023/Research/EDA Charisma Auswertung/NF/outNF.txt" "Baseline" "Event"
    twoRowsToOneRow "C:/2023/Research/EDA Charisma Auswertung/NV/outNV.txt" "Baseline" "Event"

    // doFixNeuroSession "C:/2022/Research/Neurologie/Export_MSA_ERS_17_2"

    (*
    [ 1 .. 20 ]
    |> Seq.iter( fun i -> 
        [
            sprintf "subject-%d_Antisakkaden_blocked.tsv" i
            sprintf "subject-%d_mixed1.tsv" i
            sprintf "subject-%d_mixed2.tsv" i
            sprintf "subject-%d_Prosakkaden_blocked.tsv" i
        ]
        |> Seq.iter( fixEyeTrackingFile "C:/2023/Research/HershmanAntisaccadeData" (sprintf "S%d" i) )
    )
    *)

    (*
    [
        "subject-21_mixed1.tsv"
        "subject-21_mixed2.tsv"
        "subject-22_mixed1.tsv"
        "subject-22_mixed2.tsv"
        "subject-23_mixed1.tsv"
        "subject-23_mixed2.tsv"
        "subject-24_mixed1.tsv"
        "subject-24_mixed2.tsv"
        "subject-25_mixed1.tsv"
        "subject-25_mixed2.tsv"
        "subject-26_mixed1.tsv"
        "subject-26_mixed2.tsv"
        "subject-27_mixed1.tsv"
        "subject-27_mixed2.tsv"
        "subject-28_mixed1.tsv"
        "subject-28_mixed2.tsv"
        "subject-29_mixed1.tsv"
        "subject-29_mixed2.tsv"
        "subject-30_mixed1.tsv"
        "subject-30_mixed2.tsv"
    ]
    |> Seq.iter(fixEyeTrackingFile "C:/2023/Research/GemischtGesamt" "")
    *)
    
(*
    if false then
        doEyeTrackingSession 
            "C:/2021/Research/EyeTrackingPsy/IAPS_Antisakkaden"
            [
                "Anti Arousal"
                "Anti Klassisch"
                "Anti Scramble"
                "Pro Arousal"
                "Pro Klassisch"
                "Pro Scramble"
            ]
            //  "Anti emotional 1"
            //  "Anti emotional 2"
            //  "Anti emotional 3"
            //  "Anti emotional 4"
            //  "Anti scramble"
            //  "Baseline1"
            //  "Pro classic"
            //  "Pro emotional 1"
            //  "Pro emotional 2"
            //  "Pro scramble"
    else
        doEdaSession
            "C:/2021/Research/PsyEDAledalab/EDA_Antisakkaden"
*)

    0
    *)