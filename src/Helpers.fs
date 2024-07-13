[<AutoOpen>]
module Helpers // 135

let inline sgn (x) = float (sign x)

module String =
    let makeEmptyVisible (s:string) =
        if s.Equals "" then
            "''"
        else
            s
    
let meanIfPositive (a) (b) =
    if a <= 0.0f && b > 0.0f then
        b
    elif b <= 0.0f && a > 0.0f then
        a
    elif a <= 0.0f && b <= 0.0f then
        -1.0f
    else
        0.5f * (a + b)

// -- basic type definitions
type vec3 =  
    { x: float32; y: float32; z:float32 }
    static member Zero = {x=0.0f; y=0.0f; z=0.0f}

    static member op_Addition( a, b ) =
        { x=a.x + b.x; y=a.y + b.y; z=a.z + b.z } 

    static member op_Subtraction( a, b ) =
        { x=a.x - b.x; y=a.y - b.y; z=a.z - b.z }

    static member op_Multiply ( a, b : float32 ) =
        { x = a.x*b; y = a.y*b; z = a.z*b } 

module vec3 =
    let innerProduct a b = a.x*b.x + a.y*b.y + a.z*b.z

    let norm a = sqrt(innerProduct a a)

    let angleBetween a b  = float(acos(innerProduct a b/(norm a * norm b)))*(180.0/System.Math.PI)

    let mean (a:vec3) (b:vec3) = 
        (a + b)*0.5f

    let meanPreferred (a:vec3) (b:vec3) =
        if a.x = 0.0f && b.x <> 0.0f then
            b
        elif b.x = 0.0f && a.x <> 0.0f then
            a
        else
            mean a b

    let unit a =
        let lInv = 1.f / (norm a)
        { x = a.x * lInv; y = a.y * lInv; z = a.z * lInv;  }

type ivec2 = 
    { i: int; j: int }
    static member Zero = {i=0; j=0}

type vec2 = 
    { x: float; y: float }

    static member op_Addition( a, b ) = { x=a.x + b.x; y=a.y + b.y } 
    static member op_Division( a, b ) = { x=a.x / b; y=a.y / b } 
    static member Zero =  {x = 0.0; y = 0.0}


type Range<'X when 'X : (static member Zero : 'X) and 'X : comparison> =
    {
        Start : 'X 
        End : 'X
    }
    member inline x.inside (a) =
        if a >= x.Start && a <= x.End then true else false

    static member inline Zero = 
        { 
            Start= LanguagePrimitives.GenericZero<'X>
            End= LanguagePrimitives.GenericZero<'X> 
        }


type Regression =
    { rSq : float; d : float; k : float }

    static member Zero = { rSq= 0.0; d= 0.0; k= 0.0 }

type RegressionHistory =
    {
        sum   : vec2
        sumSq : vec2
        sumCd : float
        count : int
    }
    static member Zero = {sum = vec2.Zero; sumSq= vec2.Zero; sumCd= 0.0; count= 0}

module Regression =
    let private regressionHelper len sum sumSq sumCd = 
        let ssX = sumSq.x - ((sum.x * sum.x) / len)
        let rDen = (len * sumSq.x - (sum.x * sum.x)) * (len * sumSq.y - (sum.y * sum.y))
        let sCo  = sumCd - ((sum.x * sum.y) / len)

        let mean = sum / len
        let dblR = ((len * sumCd) - (sum.x * sum.y) ) / (sqrt rDen)
        {
            rSq = dblR * dblR
            d   = mean.y - (sCo / ssX * mean.x)
            k   = sCo / ssX
        }

    let linear (vals:vec2[]) =
        let sum   = vals |> Array.sum
        let sumSq = vals |> Array.sumBy (fun v -> {x= v.x*v.x; y= v.y*v.y})
        let sumCd = vals |> Array.sumBy (fun v -> v.x * v.y)
        let len = float vals.Length

        regressionHelper len sum sumSq sumCd

    let add (v:vec2) (hist:RegressionHistory) =
        {
            sum   = hist.sum + v
            sumSq = hist.sumSq + {x= v.x*v.x; y= v.y*v.y}
            sumCd = hist.sumCd + (v.x * v.y)
            count = hist.count + 1
        }

    let toLinear (hist:RegressionHistory) =
        regressionHelper (float hist.count) hist.sum hist.sumSq hist.sumCd

    let dist (xy:vec2) (reg:Regression) =
        ((reg.k * xy.x + reg.d) - xy.y)

    let printTest () =
        // regression test
        let arr = 
            [|
                {x=141.2999955; y=528.0}
                {x=151.8499985; y=567.0}
                {x=184.399994;  y=689.0}
                {x=246.4000015; y=921.0}
                {x=251.399994;  y=940.0}
            |]

        let stat = 
            arr
            |> linear
    
        arr
        |> Array.iter( fun x -> printfn "%f -> %f" x.x (x.x*stat.k + stat.d) )

        printfn "Regression1: %A" stat

        let reg =
            seq { 0 .. arr.Length - 1 }
            |> Seq.fold (fun r i -> add arr.[i] r) RegressionHistory.Zero
        
        printfn "Regression2: %A" reg

let (|?) (optionalValue) (defaultValue) =
    match optionalValue with
    | Some x -> x
    | None -> defaultValue
