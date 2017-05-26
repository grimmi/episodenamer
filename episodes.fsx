module Episodes
#r "packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"
#load "types.fsx"
#load "tvcache.fsx"
#load "framework.fsx"
#load "showparser.fsx"

open System
open System.Net
open Newtonsoft.Json.Linq
open Newtonsoft.Json

open Types
open TvCache
open Framework
open Parser

let getEpisodePage show page = async{
    printfn "lade Seite %d von '%s'" page show.seriesName
    let client = getAuthorizedClient token
    let! response = client.AsyncDownloadString(Uri(apiUrl + "/series/" + (show.id |> string) + "/episodes?page=" + (page |> string)))
    let episodeResponse = JsonConvert.DeserializeObject<EpisodeResult>(response)

    return episodeResponse
}

let downloadEpisodes show = async {
    let! firstPage = getEpisodePage show 1
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

let getEpisodes show date = async {
        match tryGetEpisodes show with
        |true, episodes -> 
                let maxDate = episodes |> Seq.map(fun ep -> match DateTime.TryParse ep.firstAired with
                                                            |(true, d) -> d
                                                            |(false, _) -> DateTime.MinValue)
                                       |> Seq.max
                if maxDate < date then
                    let! eps = downloadEpisodes show
                    return eps
                else
                    return episodes
        |_ ->   let! episodes = downloadEpisodes show
                return episodes
}

let canonizeEpisode (episode:string) = 
    (episode |> String.filter(Char.IsLetter)).ToLower()
let matchEpisode show file = async{
    let date = parseDate file
    let! episodes = getEpisodes show date.Value
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