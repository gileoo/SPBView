#r @"packages/build/FAKE/tools/FakeLib.dll"
open Fake
open Fake.ReleaseNotesHelper
open System

Target "Clean" (fun _ ->
    !!"**/bin"
    ++"**/obj"
    |> CleanDirs
)

Target "Build" (fun _ ->
    !! "*.sln"
    |> MSBuildRelease "" "Build"
    |> ignore //Log "Build-Output: "
)

"Clean"
 ==> "Build"

RunTargetOrDefault "Build"
