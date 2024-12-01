namespace MF.AdventOfCode

module Console =
    open System.IO
    open MF.ConsoleApplication
    open MF.Utils

    let commandHelp lines = lines |> String.concat "\n\n" |> Some

    /// Concat two lines into one line for command help, so they won't be separated by other empty line
    let inline (<+>) line1 line2 = sprintf "%s\n%s" line1 line2

    (* [<RequireQualifiedAccess>]
    module Argument = *)

    [<RequireQualifiedAccess>]
    type WatchSubdirs =
        | Yes
        | No

    type FileOrDir =
        | File of string
        | Dir of string * string list

    [<RequireQualifiedAccess>]
    module FileOrDir =
        let parse (extension: string) = function
            | Some file when file |> File.Exists && file.EndsWith extension ->
                FileOrDir.File file

            | Some dir when dir |> Directory.Exists ->
                Dir (
                    dir,
                    [ dir ] |> FileSystem.getAllFiles |> List.filter (fun f -> f.EndsWith extension)
                )

            | invalidPath -> failwithf "Path to file(s) %A is invalid." invalidPath

        let debug (output: Output) title = output.Options (sprintf "%s file(s):" title) << function
            | File file -> [[ file ]]
            | Dir (_, files) -> files |> List.map List.singleton

        let file = function
            | File file -> Some file
            | _ -> None

        let files = function
            | File file -> [ file ]
            | Dir (_, files) -> files

        let watch = function
            | File file -> file, WatchSubdirs.No
            | Dir (dir, _) -> dir, WatchSubdirs.Yes

    (* [<RequireQualifiedAccess>]
    module Input =
         *)

    [<RequireQualifiedAccess>]
    module internal Watch =
        let private runForever = async {
            while true do
                do! Async.Sleep 1000
        }

        let watch (output: Output) watchSubdirs execute (path, filter) = async {
            let includeSubDirs =
                match watchSubdirs with
                | WatchSubdirs.Yes -> true
                | WatchSubdirs.No -> false

            let pathDir, fileName =
                if path |> Directory.Exists then path, None
                elif path |> File.Exists then Path.GetDirectoryName(path), Some path
                else failwithf "Path %A is invalid." path

            use watcher =
                let enable = true
                new FileSystemWatcher(
                    Path = pathDir,
                    EnableRaisingEvents = enable,
                    IncludeSubdirectories = includeSubDirs
                )

            watcher.Filters.Add(filter)

            match fileName with
            | Some fileName ->
                watcher.Filters.Add(fileName)
            | _ -> ()

            if output.IsDebug() then
                sprintf "<c:gray>[Watch]</c> Path: <c:cyan>%s</c> | Filters: <c:yellow>%s</c> | With subdirs: <c:magenta>%A</c>"
                    path
                    (watcher.Filters |> String.concat "; ")
                    includeSubDirs
                |> output.Message

            watcher.NotifyFilter <- watcher.NotifyFilter ||| NotifyFilters.LastWrite
            watcher.SynchronizingObject <- null

            let notifyWatch () =
                path
                |> sprintf "<c:gray>[Watch]</c> Watching path <c:dark-yellow>%A</c> (Press <c:yellow>ctrl + c</c> to stop) ...\n"
                |> output.Message

            let executeOnWatch event =
                if output.IsDebug() then output.Message <| sprintf "<c:gray>[Watch]</c> Source %s." event

                output.Message "<c:gray>[Watch]</c> Executing ...\n"

                try execute()
                with e -> output.Error <| sprintf "%A" e

                notifyWatch ()

            watcher.Changed.Add(fun _ -> executeOnWatch "changed")
            watcher.Created.Add(fun _ -> executeOnWatch "created")
            watcher.Deleted.Add(fun _ -> executeOnWatch "deleted")
            watcher.Renamed.Add(fun _ -> executeOnWatch "renamed")

            if output.IsVerbose() then
                output.Message <| sprintf "<c:gray>[Watch]</c> Enabled for %A" path

            notifyWatch()

            do! runForever
        }

        let executeAndWaitForWatch (output: Output) execute = async {
            try execute()
            with e -> output.Error <| sprintf "%A" e

            do! runForever
        }

    [<RequireQualifiedAccess>]
    module Assert =
        let eq expected actual =
            if expected = actual then Ok ()
            else Error <| sprintf "Value %A is not same as expected %A." actual expected
