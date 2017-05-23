#r "packages/Newtonsoft.Json/lib/net45/Newtonsoft.Json.dll"

let mutable apikey = ""
let mutable user = ""
let mutable userkey = ""

open System
open System.IO

let extractValue (line:string) = 
    (line.Split '=').[1].Trim()

let readcredentials = 
    match File.ReadAllLines("./credentials.cred") with
    |[|keyline;userline;pwline|] ->
        apikey <- keyline |> extractValue
        user <- userline |> extractValue
        userkey <- pwline |> extractValue
    |_ -> raise (Exception "invalid configuration")

readcredentials

open System.Net
open System.Text
open Newtonsoft.Json.Linq

let apiUrl = "https://api.thetvdb.com"

let login =
    use client = new WebClient()
    let body = "{\"apikey\":\"" + apikey + "\", \"username\":\"" + user + "\", \"userkey\":\"" + userkey + "\"}" 
    client.Headers.Set("Content-Type", "application/json")
    let response = client.UploadData(apiUrl + "/login", "POST",  body |> Encoding.UTF8.GetBytes)

    JObject.Parse(Encoding.UTF8.GetString response)

let token = login.["token"] |> string

printfn "%A" token

let getAuthorizedClient t =
    let client = new WebClient()
    client.Headers.Set("Content-Type", "application/json")
    client.Headers.Set("Authorization", "Bearer " + t)
    client

let searchShow show =
    let client = getAuthorizedClient token
    let response = client.DownloadString(apiUrl + "/search/series?name=" + show)

    JObject.Parse(response)

let castle = searchShow "Castle"
printfn "%s" (castle |> string)