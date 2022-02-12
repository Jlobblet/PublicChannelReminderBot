open Microsoft.Extensions.DependencyInjection
open PublicChannelReminderBot
open PublicChannelReminderBot.Settings



[<EntryPoint>]
let main argv =
    let services =
        ServiceCollection()
        |> ConfigureServices(Settings.fromArgv argv)

    Bot.MainAsync(services).GetAwaiter().GetResult()

    0
