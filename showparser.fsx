open System

let examples = [|"The_Simpsons__Dogtown_17.05.21_20-00_uswnyw_30_TVOON_DE.mpg.HQ.avi";
                 "The_Simpsons__Moho_House_17.05.07_20-00_uswnyw_30_TVOON_DE.mpg.HQ.avi";
                 "Doctor_Who_17.05.06_19-20_ukbbc_45_TVOON_DE.mpg.HQ.avi"
                 "Marvel_s_Agents_of_S_H_I_E_L_D___The_Bridge_13.12.10_20-00_uswabc_61_TVOON_DE.mpg.HQ.avi"|]

let parseShowName (file:string) = 
    if file.IndexOf "__" <> -1 then
        match file.Split([|"__"|], StringSplitOptions.RemoveEmptyEntries) with
        |[|show;_|] -> Some(show.Replace("_", " " ))
        |_ -> None
    else
        let pointIdx = file.IndexOf '.'
        Some(file.Substring(0, pointIdx - 3).Replace("_", " ").Trim())

let parseEpisodeName (file:string) = 
    let pointIdx = file.IndexOf '.'
    let dblUnderscoreIdx = file.IndexOf "__"

    if pointIdx = -1 || dblUnderscoreIdx = -1 then
        None
    else
        let length = (pointIdx - 3) - (dblUnderscoreIdx + 2)
        match length with
        |_ when length > 0 -> Some(file.Substring(dblUnderscoreIdx + 2, length).Replace("_", " ").Trim())
        |_ -> None

let parseDate (file:string) = 
    let pointIdx = file.IndexOf '.'
    match pointIdx with
    |(-1) -> None
    |_ -> 
        let datePart = file.Substring(pointIdx - 2, 8)
        Some(DateTime.ParseExact(datePart, "yy.MM.dd", null))


let infos = examples |> Array.map(fun ex -> (ex |> parseShowName, ex |> parseEpisodeName, ex |> parseDate))