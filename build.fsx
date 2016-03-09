// include Fake libs
#r "src/packages/FAKE/tools/FakeLib.dll"

open Fake
open Fake.Paket
open Fake.FileUtils
open Fake.Testing.XUnit2

// Directories
let rootDir = currentDirectory
let buildDir  = currentDirectory + "/bin/"
let nugetDir = currentDirectory + "/src/nuget/"
let testOutputDir = currentDirectory + "/"

let nugetApiKey = environVar "BAMBOO_nugetApiKey"
let nugetVersion = getBuildParamOrDefault "nugetVersion" null

// Filesets
let appReferences  =
    !! "src/**/*.csproj"
      ++ "src/**/*.fsproj"

// Targets
Target "Clean" (fun _ ->
    CleanDirs [buildDir; nugetDir]
    MSBuildRelease buildDir "Clean" appReferences
        |> Log "Clean-Output: "
)

Target "BuildApp" (fun _ ->
    // compile all projects below src
    MSBuildRelease buildDir "Build" appReferences
        |> Log "BuildApp-Output: "
)

Target "Push" (fun _ ->
    Push (fun p ->
        {p with
            ApiKey = nugetApiKey
            WorkingDir = "nuget"
        })
)

Target "Pack" (fun _ ->
    Pack (fun p ->
        let p' = {p with OutputPath = nugetDir; WorkingDir = rootDir + "/src" }
        if nugetVersion = null then p' else {p' with Version = nugetVersion }
        )
)

Target "Test" (fun _ ->
    !! ("src/**/NUnit.Test.*.dll")
      |> NUnit (fun p ->
          {p with
             DisableShadowCopy = true;
             OutputFile = rootDir + "/TestResults.xml" })
)

// Build order
"Clean"
  ==> "BuildApp"
  ==> "Test"
  ==> "Pack"
  ==> "Push"

// start build
RunTargetOrDefault "Test"
