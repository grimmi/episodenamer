#r "packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"
#load "showparser.fsx"
#load "types.fsx"
#load "tvcache.fsx"

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

let mutable apikey = ""
let mutable user = ""
let mutable userkey = ""
let mutable token = ""


let extractValue (line:string) = 
    (line.Split '=').[1].Trim()

let readcredentials = 
    match File.ReadAllLines("./credentials.cred") |> Array.take 3 with
    |[|keyline;userline;pwline|] ->
        apikey <- keyline |> extractValue
        user <- userline |> extractValue
        userkey <- pwline |> extractValue
    |_ -> raise (Exception "invalid configuration")

readcredentials

let apiUrl = "https://api.thetvdb.com"

let login = async {
        use client = new WebClient()
        let body = "{\"apikey\":\"" + apikey + "\", \"username\":\"" + user + "\", \"userkey\":\"" + userkey + "\"}" 
        client.Headers.Set("Content-Type", "application/json")
        let! response = client.UploadDataTaskAsync(apiUrl + "/login", "POST",  body |> Encoding.UTF8.GetBytes) |> Async.AwaitTask
        let jsonToken = JObject.Parse(Encoding.UTF8.GetString response)
        return jsonToken.["token"] |> string }     

let getAuthorizedClient t =
    let client = new WebClient()
    client.Headers.Set("Content-Type", "application/json")
    client.Headers.Set("Authorization", "Bearer " + t)
    client.Headers.Set("Accept-Language", "en")
    client

let searchShow show = async {
            let client = getAuthorizedClient token
            let! response = client.AsyncDownloadString(Uri(apiUrl + "/search/series?name=" + show))
            return JsonConvert.DeserializeObject<SearchResponse>(response) }

let getEpisodePage show page = async{
    let client = getAuthorizedClient token
    let! response = client.AsyncDownloadString(Uri(apiUrl + "/series/" + (show.id |> string) + "/episodes?page=" + (page |> string)))
    let episodeResponse = JsonConvert.DeserializeObject<EpisodeResult>(response)

    return episodeResponse
}

let getEpisodes show = async {
        match tryGetEpisodes show with
        |true, episodes -> return episodes
        |_ ->   let! firstPage = getEpisodePage show 1
                if firstPage.links.last.HasValue && firstPage.links.last.Value > 1 then
                    let additionalEpisodes =    [ 2 .. firstPage.links.last.Value ]
                                                |> Seq.collect(fun page ->  let pageResult = (getEpisodePage show page) |> Async.RunSynchronously
                                                                            pageResult.data)
                    let episodes = Seq.concat [firstPage.data; additionalEpisodes |> Array.ofSeq]
                    show |> cacheEpisodes episodes
                    return episodes
                else
                    show |> cacheEpisodes firstPage.data
                    return firstPage.data |> Seq.ofArray
}

let files = [|  "Marvel_s_Agents_of_S_H_I_E_L_D___The_Bridge_13.12.10_20-00_uswabc_61_TVOON_DE.mpg.HQ.avi";
                "The_Simpsons__Dogtown_17.05.21_20-00_uswnyw_30_TVOON_DE.mpg.HQ.avi";
                "The_Simpsons__Moho_House_17.05.07_20-00_uswnyw_30_TVOON_DE.mpg.HQ.avi";
                "Doctor_Who_17.05.06_19-20_ukbbc_45_TVOON_DE.mpg.HQ.avi" |]

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

let canonizeEpisode (episode:string) = 
    (episode |> String.filter(Char.IsLetter)).ToLower()

let matchEpisode show file = async{
    let date = parseDate file
    let! episodes = getEpisodes show
    let episodeName = parseEpisodeName file
    let matchingEpisode = episodes |> Seq.tryFind(fun e -> 
        match (episodeName, date) with
        |(None, Some d) -> not (isNull e.firstAired) && e.firstAired.Length > 0 && DateTime.Parse(e.firstAired) = d
        |(Some n, Some d) -> (canonizeEpisode n) = (canonizeEpisode e.episodeName)
                             || not (isNull e.firstAired) && e.firstAired.Length > 0 && DateTime.Parse(e.firstAired) = d
        |(Some n, None) -> (canonizeEpisode n) = (canonizeEpisode e.episodeName)
        |(None, None) -> false)

    return matchingEpisode
}

let rec findShow showName =

    let rec findShowInDb original current = async{
        try
            match tryGetShow current with
            |true, mappedShow -> 
                            printfn "Nehme Show aus Cache: %s" mappedShow.seriesName
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


let findEpisode file = async {
    fillShowCache
    let! loggedIn = login
    token <- loggedIn
    let parsedName = file |> parseShowName
    let! show = findShow parsedName
    match show with
    |None -> printfn "Keine Show gefunden!"
    |Some s ->  let! episode = matchEpisode s file
                match episode with
                | None -> printfn "Episode konnte nicht zugeordnet werden"
                | Some ep -> 
                    let info = sprintf "%s %dx%d %s" s.seriesName ep.airedSeason.Value ep.airedEpisodeNumber.Value ep.episodeName
                    printfn "%s" info
}

for ep in files do
    findEpisode ep |> Async.RunSynchronously