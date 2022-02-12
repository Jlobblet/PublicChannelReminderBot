module PublicChannelReminderBot.Extensions

open System
open System.Threading.Tasks

type IServiceProvider with
    member this.GetService<'a>() = this.GetService typedefof<'a> :?> 'a

[<RequireQualifiedAccess>]
module Task =
    let waitUntil (interval: int) condition =
        task {
            while not <| condition () do
                do! Task.Delay interval
        }
        :> Task
