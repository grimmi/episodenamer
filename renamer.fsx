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

let apiUrl = "https://api.thetvdb.com"

let login =
    use client = new WebClient()
    let body = "{\"apikey\":\"" + apikey + "\", \"username\":\"" + user + "\", \"userkey\":\"" + userkey + "\"}" 
    printfn "body: %s" body
    client.Headers.Set("Content-Type", "application/json")
    let response = client.UploadData(apiUrl + "/login", "POST",  body |> Encoding.UTF8.GetBytes)

    System.Text.Encoding.UTF8.GetString response

let token = login