module Excel // 163

open ETData
open ETReaction
open OxyPlot
open System
open System.IO
open System.Windows.Forms
open OfficeOpenXml

let saveSession (s:Session) (filterTargets) (filename) =
    let rec loop i max lstEval lstBlinks=
        if i <> max then 
            let vTargets = s.Targets.ChangesValid
            let evalData = evalTargetEvents s i
            let intervals = baseIntervalsFromTarget evalData s.DataConfig s.Targets.ChangesValid.[i] 
            let blinkIntervals = blinkIntervalsFromTarget evalData s.Targets.ChangesValid.[i]
            let target       = snd vTargets.[i]
            let targetStart    = s.EyeData.[fst vTargets.[i]].timeTarget.TimeStamp
            let targetAnalysisData = analyseIntervalsForTarget intervals (getErrorNr i (s.Errors)) i target.FullName targetStart

            let blinks = 
                blinkIntervals
                |> List.filter( fun x -> x.tType = EyeEvent.Blink )  

            if not targetAnalysisData.IsEmpty then
                loop (i+1) max (lstEval@targetAnalysisData) (lstBlinks@blinks)
            else 
                loop (i+1) max (lstEval) (lstBlinks@blinks)
        else
            (lstEval, lstBlinks)
        
    let eAData, blinks = loop 0 (s.Targets.ChangesValid.Length) List<EventAnalysisData>.Empty List<IntervalData>.Empty  

    // --prepare file
    if File.Exists( filename ) then File.Delete( filename )
            
    let newFile = new FileInfo( filename )
    use pck = new ExcelPackage( newFile )
    let ws  = pck.Workbook.Worksheets.Add( "GazeMovement" )
    ws.Cells.["A1"].Value <- "PatientID"
    ws.Cells.["A2"].Value <- s.Name

    [1..15]
    |>Seq.iter( fun i ->  ws.Column(i).Width <- 12.0 )
    ws.Column(2).Width <- 13.0
    ws.Column(3).Width <- 14.5
    ws.Column(6).Width <- 13.0

    //-- headers
    ws.Cells.["A11"].Value <- "Event"
    ws.Cells.["B11"].Value <- "Substr1"
    ws.Cells.["C11"].Value <- "SubStr2"
    ws.Cells.["D11"].Value <- "Label"
    ws.Cells.["E11"].Value <- "ReactionTime"
    ws.Cells.["F11"].Value <- "TimeToFixationTarget"
    ws.Cells.["G11"].Value <- "TimeToFixation"
    ws.Cells.["H11"].Value <- "XErrorAtFixation"

    ws.Cells.["J11"].Value <- "GainFirst"
    ws.Cells.["K11"].Value <- "GainBest"
    ws.Cells.["L11"].Value <- "NrOfCorrSacc"
    ws.Cells.["M11"].Value <- "SpeedMean"
    ws.Cells.["N11"].Value <- "SpeedMax"
    ws.Cells.["O11"].Value <- "Error(0-4;9)"
    ws.Cells.["P11"].Value <- "TargetIndex"

    ws.Cells.["C3"].Value <- "Rt"
    ws.Cells.["C4"].Value <- "Left"
    ws.Cells.["D3"].Value <- "Rt"
    ws.Cells.["D4"].Value <- "Right"
    ws.Cells.["E3"].Value <- "Rt"
    ws.Cells.["F4"].Value <- "TtFT"
    ws.Cells.["G4"].Value <- "TfF"
    ws.Cells.["H4"].Value <- "XErrF"

    ws.Cells.["J4"].Value <- "GfAbs"
    ws.Cells.["K4"].Value <- "GbAbs"
    ws.Cells.["M4"].Value <- "SpdMean"
    ws.Cells.["N4"].Value <- "SpdMax"

    // ws.Cells.["J3"].Value <- "RtAfterErr"
    // ws.Cells.["K3"].Value <- "RtBeforeErr"

    ws.Cells.["O4"].Value <- "NrErrTotal"
    ws.Cells.["O7"].Value <- "NrBadData"

    ws.Cells.["A5"].Value <- "Mean"
    ws.Cells.["A6"].Value <- "Stdev"
    ws.Cells.["A7"].Value <- "Min"
    ws.Cells.["A8"].Value <- "Max"

    [4; 5; 9]
    |> Seq.iter( fun x -> ws.Column(x).Style.Numberformat.Format <- "0.00" )

    [10; 11; 12]
    |> Seq.iter( fun x -> ws.Column(x).Style.HorizontalAlignment <- Style.ExcelHorizontalAlignment.Center )

    let startRow = 12
    // -- do data save here

    let mutable count = 0
    eAData
    |> Seq.iteri( fun i x ->
        let j = count + startRow 
        let targetIdx, target = x.target

        // -- extract time comments marked with '+' at start
        let mutable timeCommentIdx = -1
        let mutable lastIdx = -1;
        s.TimeComment
        |> Array.indexed
        |> Array.filter( fun (i, (k,v)) -> v.Chars(0) = '+' )
        |> Array.iter( fun (i, (k,v)) -> 
//                printfn "%A, %A" k v
            if k >= x.timeStamp && timeCommentIdx = -1 && v.Chars( 0 ) = '+' then
//                    printfn "  Chosen!"
                timeCommentIdx <- lastIdx
            else
                lastIdx <- i )
         
        let timeComment = 
            if s.Targets.Labels.IsSome then 
                let _, txt, _ = s.Targets.Labels.Value.[x.targetNr]
                if txt.[0] = '+' then txt.Substring(1) else txt 
            else "-"

(*            if timeCommentIdx > 0 then
                snd s.TimeComment.[timeCommentIdx]
            else if timeCommentIdx = 0 then
                snd s.TimeComment.[0]
            else
                "-"
*)
        if target.IDName |> isDesiredTarget filterTargets then
            ws.Cells.[ (sprintf "A%d" j) ].Value   <- target.IDName
            ws.Cells.[ (sprintf "B%d" j) ].Formula <- (sprintf "=LEFT(A%d, 2)" j)
            ws.Cells.[ (sprintf "C%d" j) ].Formula <- (sprintf "=MID(A%d, 3, 3)" j)
            ws.Cells.[ (sprintf "D%d" j) ].Value   <- timeComment

            ws.Cells.[ (sprintf "E%d" j) ].Value <- 
                //-- do user correction
                let reactionTimeStamps = s.UserCorrections.reactionTime.TryFind x.targetNr
                if reactionTimeStamps.IsSome then 
                    reactionTimeStamps.Value.y - reactionTimeStamps.Value.x
                    else
                        x.reactionTime  
            ws.Cells.[ (sprintf "F%d" j) ].Value <- x.timeToFirstFixationTarget 
            ws.Cells.[ (sprintf "G%d" j) ].Value <- x.timeToFirstFixation 
            ws.Cells.[ (sprintf "H%d" j) ].Value <- x.firstFixationXError 
            ws.Cells.[ (sprintf "J%d" j) ].Value <- x.gainFirst 
            ws.Cells.[ (sprintf "K%d" j) ].Value <- x.gainBest 
            ws.Cells.[ (sprintf "L%d" j) ].Value <- x.nrOfCorrectionSaccades 
            ws.Cells.[ (sprintf "M%d" j) ].Value <- x.speed.mean
            ws.Cells.[ (sprintf "N%d" j) ].Value <- x.speed.max
            ws.Cells.[ (sprintf "O%d" j) ].Value <- x.error  
            ws.Cells.[ (sprintf "P%d" j) ].Value <- x.targetNr + 1
            count <- count + 1
    )
                     
         
    // ws.Calculate()     
          
    let endRow = (eAData.Length+startRow-1)  

    let blockStats s = 
        ws.Cells.[sprintf "%s5" s].Formula <- (sprintf "=AVERAGEIFS(%s12:%s%d,O12:O%d,0)" s s endRow endRow)
        ws.Cells.[sprintf "%s6" s].CreateArrayFormula (sprintf "=STDEV.P(IF(O12:O%d=0,%s12:%s%d))" endRow s s endRow)
        ws.Cells.[sprintf "%s7" s].CreateArrayFormula (sprintf "=MIN(IF(O12:O%d=0,%s12:%s%d))" endRow s s endRow)
        ws.Cells.[sprintf "%s8" s].CreateArrayFormula (sprintf "=MAX(IF(O12:O%d=0,%s12:%s%d))" endRow s s endRow)

//                let fRtMeanCueR = (sprintf "=SUMIFS(B9:B%d,A9:A%d,\"CueR\",G9:G%d,\"n\") / COUNTIFS(A9:A%d,\"CueR\",G9:G%d,\"n\")"  endRow endRow endRow endRow endRow )

    ws.Cells.["C5"].Formula <- (sprintf "=AVERAGEIFS(E12:E%d,A12:A%d,C4,O12:O%d,0)"  endRow endRow endRow)
    ws.Cells.["C6"].CreateArrayFormula (sprintf "=STDEV.P(IF(O12:O%d=0,IF(A12:A%d=C4,E12:E%d)))" endRow endRow endRow)
    ws.Cells.["C7"].CreateArrayFormula (sprintf "=MIN(IF(O12:O%d=0,IF(LEFT(A12:A%d, 4)=C4,E12:E%d)))" endRow endRow endRow)
    ws.Cells.["C8"].CreateArrayFormula (sprintf "=MAX(IF(O12:O%d=0,IF(A12:A%d=C4,E12:E%d)))" endRow endRow endRow)

    ws.Cells.["D5"].Formula <- (sprintf "=AVERAGEIFS(E12:E%d,A12:A%d,D4,O12:O%d,0)"  endRow endRow endRow)
    ws.Cells.["D6"].CreateArrayFormula (sprintf "=STDEV.P(IF(O12:O%d=0,IF(A12:A%d=D4,E12:E%d)))" endRow endRow endRow)
    ws.Cells.["D7"].CreateArrayFormula (sprintf "=MIN(IF(O12:O%d=0,IF(A12:A%d=D4,E12:E%d)))" endRow endRow endRow)
    ws.Cells.["D8"].CreateArrayFormula (sprintf "=MAX(IF(O12:O%d=0,IF(A12:A%d=D4,E12:E%d)))" endRow endRow endRow)
           
    blockStats "E" 
    blockStats "F"
    blockStats "G"
    blockStats "H" 
    
    (*
    ws.Cells.["E5"].Formula <- (sprintf "=AVERAGEIFS(E12:E%d,O12:O%d,0)" endRow endRow)
    ws.Cells.["E6"].CreateArrayFormula (sprintf "=STDEV.P(IF(O12:O%d=0,E12:E%d))" endRow endRow)
    ws.Cells.["E7"].CreateArrayFormula (sprintf "=MIN(IF(O12:O%d=0,E12:E%d))" endRow endRow)
    ws.Cells.["E8"].CreateArrayFormula (sprintf "=MAX(IF(O12:O%d=0,E12:E%d))" endRow endRow)
    *)

    ws.Cells.["O5"].Formula <- (sprintf "=COUNTIF(O12:O%d,\">0\")-COUNTIF(O12:O%d,\">8\")" endRow endRow)
    ws.Cells.["O5"].Style.Numberformat.Format <- "0"

    ws.Cells.["O8"].Formula <- (sprintf "=COUNTIF(O12:O%d,\">8\")" endRow)
    ws.Cells.["O8"].Style.Numberformat.Format <- "0"

    let fGfMean = (sprintf "=(SUMIFS(J12:J%d,O12:O%d,0,J12:J%d,\">=0\") - SUMIFS(J12:J%d,O12:O%d,0,J12:J%d,\"<0\")) / (%d - O5)" 
                    endRow endRow endRow endRow endRow endRow eAData.Length)
    let fGbMean = (sprintf "=(SUMIFS(K12:K%d,O12:O%d,0,K12:K%d,\">=0\") - SUMIFS(K12:K%d,O12:O%d,0,K12:K%d,\"<0\")) / (%d - O5)" 
                    endRow endRow endRow endRow endRow endRow eAData.Length)
    ws.Cells.["J5"].Formula <- fGfMean
    ws.Cells.["J6"].CreateArrayFormula (sprintf "=STDEV.P(IF(O12:O%d=0,ABS(J12:J%d)))" endRow endRow)
    ws.Cells.["J7"].CreateArrayFormula (sprintf "=MIN(IF(O12:O%d=0,ABS(J12:J%d)))" endRow endRow)
    ws.Cells.["J8"].CreateArrayFormula (sprintf "=MAX(IF(O12:O%d=0,ABS(J12:J%d)))" endRow endRow)

    ws.Cells.["K5"].Formula <- fGbMean
    ws.Cells.["K6"].CreateArrayFormula (sprintf "=STDEV.P(IF(O12:O%d=0,ABS(K12:K%d)))" endRow endRow)
    ws.Cells.["K7"].CreateArrayFormula (sprintf "=MIN(IF(O12:O%d=0,ABS(K12:K%d)))" endRow endRow)
    ws.Cells.["K8"].CreateArrayFormula (sprintf "=MAX(IF(O12:O%d=0,ABS(K12:K%d)))" endRow endRow)

    blockStats "M"
    blockStats "N" 

    ws.Calculate()


    // --prepare file for blinks
    let wsB  = pck.Workbook.Worksheets.Add( "Blinks" )
    wsB.Cells.["A1"].Value <- "BlinkNr"
    wsB.Cells.["B1"].Value <- "Start"
    wsB.Cells.["C1"].Value <- "End"
    wsB.Cells.["D1"].Value <- "Duration (ms)"
    wsB.Cells.["E1"].Value <- "Inbetween (s)"
    wsB.Cells.["F1"].Value <- "TargetID"
    wsB.Cells.["G1"].Value <- "Target Nr"

    let blinksArr = blinks |> Seq.toArray

    wsB.Column(1).Style.Numberformat.Format <- "0"
    [2 .. 6]
    |> Seq.iter( fun i -> 
        wsB.Column(i).Style.Numberformat.Format <- "0.00" 
        wsB.Column(i).Width <- 15.0 )

    blinksArr
    |> Array.iteri( fun i x ->
        wsB.Cells.[ (sprintf "A%d" (i+2)) ].Value <- i+1
        wsB.Cells.[ (sprintf "B%d" (i+2)) ].Value <- x.tStart.x
        wsB.Cells.[ (sprintf "C%d" (i+2)) ].Value <- x.tEnd.x
        wsB.Cells.[ (sprintf "D%d" (i+2)) ].Value <- x.tEnd.x - x.tStart.x 
        wsB.Cells.[ (sprintf "E%d" (i+2)) ].Value <- 
            if i > 0 then
                (x.tStart.x - blinksArr.[i-1].tEnd.x) / 1000.0
            else 
                0.0    
        wsB.Cells.[ (sprintf "F%d" (i+2)) ].Value <- (snd x.target).IDName
        wsB.Cells.[ (sprintf "G%d" (i+2)) ].Value <- (fst x.target)
    )

    pck.Save()
