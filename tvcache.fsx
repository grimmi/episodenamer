module TvCache
#load "types.fsx"

open System
open System.Collections.Generic
open System.IO
open Types

let private showMap = Dictionary<string, Show>()
let private showEpisodes = Dictionary<Show, Episode seq>()

let tryGetShow show = showMap.TryGetValue show
let tryGetEpisodes show = showEpisodes.TryGetValue show

let cacheEpisodes episodes show = 
    showEpisodes.[show] <- episodes

let deserializeShow (line: string) = 
    let parts = line.Split([|"->"|], StringSplitOptions.RemoveEmptyEntries)
    let parsedName = parts.[0].Trim()
    let values = parts.[1].Split([|"***"|], StringSplitOptions.RemoveEmptyEntries)
    (parsedName, { id = values.[1].Trim() |> int; seriesName = values.[0].Trim(); aliases = [|""|]})

let fillShowCache =
    File.ReadAllLines("./shows.mapping")
    |> Seq.filter(fun line -> line |> String.length > 0)
    |> Seq.map deserializeShow
    |> Seq.iter(fun (key, show) -> showMap.[key] <- show)

let cacheShow parsedName foundShow = 
    showMap.[parsedName] <- foundShow
    let mapping = sprintf "%s -> %s *** %d" parsedName foundShow.seriesName foundShow.id
    if not (File.ReadAllLines("./shows.mapping") |> Seq.exists(fun line -> line = mapping)) then
        File.AppendAllText("./shows.mapping", Environment.NewLine + mapping)