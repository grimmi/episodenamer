#r "packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"
#r "OtrBatchDecoder.dll"
#load "showparser.fsx"
#load "types.fsx"
#load "tvcache.fsx"
#load "episodes.fsx"
#load "framework.fsx"

open System
open System.Collections.Generic
open System.Globalization
open System.IO
open System.Net
open System.Text
open Newtonsoft.Json.Linq
open Newtonsoft.Json

open TvCache
open Parser
open Types
open OtrBatchDecoder
open Episodes
open Framework


let searchShow show = async {
            let client = getAuthorizedClient token
            let! response = client.AsyncDownloadString(Uri(apiUrl + "/search/series?name=" + show))
            return JsonConvert.DeserializeObject<SearchResponse>(response) }

let choose (options: (int*string) seq) =
    match options with
    |_ when Seq.length options = 1 -> 
            printfn "automatically chose first option"
            0
    |_ ->   options
            |> Seq.iter(fun (idx, desc) -> printfn "[%d]: %s" idx desc)
            printf "Which option? "
            let input = Console.ReadLine()    
            printfn "your choice: %s" input
            input |> int

let rec findShow showName =

    let rec findShowInDb original current = async{
        try
            match tryGetShow current with
            |true, mappedShow -> 
                            //printfn "Nehme Show aus Cache: %s" mappedShow.seriesName
                            return Some(mappedShow)
            |false, _ ->    let! shows = searchShow current
                            let chosenIdx = choose (shows.data |> Seq.mapi(fun idx show -> (idx, show.seriesName)))
                            let chosenShow = shows.data.[chosenIdx]
                            cacheShow original chosenShow      
                            return Some(chosenShow)
        with
            | :? WebException as ex ->  printfn "Konnte zu '%s' keine Show ermitteln" current
                                        printfn "Unbekannte Show, bitte Namen eingeben:"
                                        let newShowName = Console.ReadLine()
                                        if newShowName <> "x" then
                                            let! nextShow = findShowInDb original newShowName
                                            return nextShow
                                        else
                                            return None
    }
    match showName with
    |None -> async { return  None }
    |Some n -> findShowInDb n n


let findEpisode file postprocess = async {
    fillShowCache
    let parsedName = file |> parseShowName
    let! show = findShow parsedName
    match show with
    |None -> printfn "Keine Show gefunden! ('%s')" file
    |Some s ->  loadEpisodes s
                let! episode = matchEpisode s file
                match episode with
                | None -> printfn "Episode konnte nicht zugeordnet werden"
                | Some ep -> 
                    let info = sprintf "%s %02dx%02d %s" s.seriesName ep.airedSeason.Value ep.airedEpisodeNumber.Value ep.episodeName
                    printfn "%s" info
                    postprocess file s ep
}

let target = "z:\\downloads\\temp"
let move newdir file show ep =
    if not (Directory.Exists newdir) then
        (Directory.CreateDirectory newdir) |> ignore

    let epName = sprintf "%s %02dx%02d %s" show.seriesName ep.airedSeason.Value ep.airedEpisodeNumber.Value ep.episodeName
    let validName = epName |> String.filter(fun c -> (Path.GetInvalidFileNameChars() |> Seq.contains c))
    let newPath = Path.Combine(newdir, epName + ".avi")
    printfn "copying '%s' to '%s'" file newPath
    File.Copy(file, newPath, true)

let postCopy = move target 

// let decoderOptions = OtrBatchDecoder.DecoderOptions()
// decoderOptions.AutoCut <- true
// decoderOptions.ContinueWithoutCutlist <- true
// decoderOptions.CreateDirectories <- true
// decoderOptions.DecoderPath <- @"Z:\Downloads\OTR\DecoderCLI\otrdecoder.exe"
// decoderOptions.Email <- File.ReadAllLines("./otrcredentials.cred").[0].Split('=').[1]
// decoderOptions.FileExtensions <- [|".otrkey"|]
// decoderOptions.ForceOverwrite <- true
// decoderOptions.InputDirectory <- @"Z:\Downloads\fstest"
// decoderOptions.OutputDirectory <- @"Z:\Downloads\fstest\fsdecoded"
// decoderOptions.Password <- File.ReadAllLines("./otrcredentials.cred").[1].Split('=').[1]

// let decoder = OtrBatchDecoder()
// let decoded = decoder.Decode decoderOptions

// let files = decoded

login
let files = Directory.GetFiles(@"z:\downloads\fstest", "*.otrkey")

for ep in files |> Seq.sort do
    (findEpisode ep postCopy) |> Async.RunSynchronously