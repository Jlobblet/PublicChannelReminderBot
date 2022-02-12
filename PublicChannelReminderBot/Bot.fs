module PublicChannelReminderBot.Bot

open System
open System.Threading.Tasks
open DisCatSharp
open PublicChannelReminderBot.Extensions
open PublicChannelReminderBot.Settings
open PublicChannelReminderBot.Cache

let MainAsync (services: IServiceProvider) =
    let settings = services.GetService<Settings>()

    let discordConfig = DiscordConfiguration()
    discordConfig.AutoReconnect <- true
    discordConfig.Token <- settings.DiscordToken

    discordConfig.Intents <-
        DiscordIntents.AllUnprivileged
        ||| DiscordIntents.GuildMessages

    let client = new DiscordClient(discordConfig)
    let { Token = killSwitch } = services.GetService<KillSwitch>()

    let cache = Cache.createDefault client
    client.add_MessageCreated (Cache.messageCreated cache)


    task {
        do! client.ConnectAsync()
        do! Task.Run(Cache.checkLoop cache)
        do! Task.waitUntil 1000 (fun () -> killSwitch.IsCancellationRequested)
        do! client.DisconnectAsync()
    }
    :> Task
