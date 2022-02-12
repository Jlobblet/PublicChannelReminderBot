module PublicChannelReminderBot.Cache

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Text.Json
open System.IO
open System.Threading
open System.Threading.Tasks
open DisCatSharp
open DisCatSharp.Entities
open DisCatSharp.EventArgs

// These aliases make the following types easier to reason about
type GuildId = uint64
type ChannelId = uint64
type LastReminderTime = DateTimeOffset option
type CacheDictionary = ConcurrentDictionary<GuildId, ConcurrentDictionary<ChannelId, LastReminderTime * ConcurrentQueue<DiscordMessage>>>

type ChannelConfig = Dictionary<GuildId, ChannelId []>

[<RequireQualifiedAccess>]
module CacheDictionary =
    let createFromConfig filepath =
        let channelConfig =
            filepath
            |> File.ReadAllText
            |> JsonSerializer.Deserialize<ChannelConfig>

        let trueOrCrash msg b = if not b then failwith msg
        let cache = CacheDictionary()
        // If any part of this setup fails, just crash
        for kvp in channelConfig do
            let serverId = kvp.Key

            cache.TryAdd(serverId, ConcurrentDictionary<ChannelId, LastReminderTime * ConcurrentQueue<DiscordMessage>>())
            |> trueOrCrash $"Failed to add cache for server %i{serverId}"

            let d = cache[serverId]

            for channelId in kvp.Value do
                d.TryAdd(channelId, (None, ConcurrentQueue<DiscordMessage>()))
                |> trueOrCrash $"Failed to create message queue for channel %i{channelId} in server %i{serverId}"

        cache

    let createDefault () = createFromConfig "channels.json"

type Cache =
    { Client: DiscordClient
      CacheDictionary: CacheDictionary
      Cancel: CancellationTokenSource
      KeepDuration: TimeSpan
      ReminderInterval: TimeSpan
      MaxMessageCount: int }

[<RequireQualifiedAccess>]
module Cache =
    let private keepDuration = TimeSpan.FromSeconds 60
    let private reminderInterval = TimeSpan.FromMinutes 30
    let private maxMessageCount = 12

    let private reminderText =
        "https://cdn.discordapp.com/attachments/292281159495450625/942136988742590514/unknown.png"

    let private addMessage cache (message: DiscordMessage) =
        task {
            let guild = message.Channel.Guild
            if isNull guild then return ()
            match cache.CacheDictionary.TryGetValue guild.Id with
            | true, channels ->
                match channels.TryGetValue message.Channel.Id with
                | true, (_, queue) -> queue.Enqueue message
                | _ -> ()
            | _ -> ()
        }
        :> Task

    let messageCreated cache (_: DiscordClient) (args: MessageCreateEventArgs) = addMessage cache args.Message

    let createDefault client =
        { Client = client
          CacheDictionary = CacheDictionary.createDefault()
          Cancel = new CancellationTokenSource()
          KeepDuration = keepDuration
          ReminderInterval = reminderInterval
          MaxMessageCount = maxMessageCount }

    let sendReminder cache guildId channelId =
        cache
            .Client
            .Guilds[guildId]
            .Channels[channelId]
            .SendMessageAsync reminderText

    let checkCache cache =
        let currentTime = DateTimeOffset.Now

        let checkQueue guildId channelId (lastReminderTime: DateTimeOffset option, queue: ConcurrentQueue<DiscordMessage>) =
            // Purge old messages
            while
                match queue.TryPeek() with
                | true, message when currentTime - message.CreationTimestamp >= cache.KeepDuration -> true
                | _ -> false
                do
                queue.TryDequeue() |> ignore<bool * DiscordMessage>

            // Count the number of messages left
            let count = queue.Count
            // Check we can remind
            let shouldRemind = Option.exists (fun t -> currentTime - t >= cache.ReminderInterval) lastReminderTime

            // If over the limit, send reminder
            if shouldRemind && count > cache.MaxMessageCount then
                sendReminder cache guildId channelId :> Task
            else
                Task.CompletedTask

        task {
            for guildKvp in cache.CacheDictionary do
                let guildId = guildKvp.Key

                for channelKvp in guildKvp.Value do
                    do! checkQueue guildId channelKvp.Key channelKvp.Value
        }
        :> Task

    let checkLoop cache () =
        task {
            while not <| cache.Cancel.IsCancellationRequested do
                do! checkCache cache
                do! Task.Delay 5000
        }
        :> Task
