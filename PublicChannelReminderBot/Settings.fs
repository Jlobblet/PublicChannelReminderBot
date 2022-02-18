module PublicChannelReminderBot.Settings

open System
open System.Threading
open Argu
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection

type HostedConfigurationReader(config: IConfiguration) =
    interface IConfigurationReader with
        member this.GetValue(k) = config.[k]
        member this.Name = ".NET Core hosted configuration"

type IServiceCollection with
    member this.AddArgu<'T when 'T: not struct and 'T :> IArgParserTemplate>
        (configure: IConfigurationReader -> ParseResults<'T>)
        =
        this
            .AddSingleton<IConfigurationReader, HostedConfigurationReader>()
            .AddSingleton<ParseResults<'T>>(fun services ->
                services.GetRequiredService<IConfiguration>()
                |> HostedConfigurationReader
                |> configure)

    member this.AddArgu<'T when 'T: not struct and 'T :> IArgParserTemplate>() =
        this.AddArgu<'T> (fun config ->
            ArgumentParser
                .Create<'T>()
                .Parse(configurationReader = config))


[<NoComparison>]
type Arguments =
    | [<ExactlyOnce>] DiscordToken of DiscordToken: string
    | MessageKeepDuration of int
    | ReminderInterval of int
    | MessagesToRemind of int
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | DiscordToken _ -> ""
            | MessageKeepDuration _ -> ""
            | ReminderInterval _ -> ""
            | MessagesToRemind _ -> ""


let errorHandler =
    ProcessExiter(
        colorizer =
            function
            | ErrorCode.HelpText -> None
            | _ -> Some ConsoleColor.Red
    )

let Parser = ArgumentParser.Create<Arguments>(errorHandler = errorHandler)

[<NoComparison>]
type Settings =
    { DiscordToken: string
      MessageKeepDuration: TimeSpan
      ReminderInterval: TimeSpan
      MaxMessageCount: int
      Configuration: IConfiguration }

[<RequireQualifiedAccess>]
module Settings =
    [<Literal>]
    let private configFile = "appsettings.json"

    let fromArgv argv =
        let config =
            ConfigurationBuilder()
                .AddJsonFile(configFile)
                .Build()

        let results =
            Parser.Parse(inputs = argv, configurationReader = HostedConfigurationReader(config))
            
        let discordToken = results.GetResult <@ DiscordToken @>
        
        let messageKeepDuration =
            results.TryGetResult <@ MessageKeepDuration @>
            |> Option.fold (fun _ -> TimeSpan.FromSeconds) (TimeSpan.FromSeconds 60)
            
        let reminderInterval =
            results.TryGetResult <@ ReminderInterval @>
            |> Option.fold (fun _ -> TimeSpan.FromMinutes) (TimeSpan.FromMinutes 30)
            
        let maxMessageCount =
            results.TryGetResult <@ MessagesToRemind @>
            |> Option.defaultValue 12

        { DiscordToken = discordToken
          MessageKeepDuration = messageKeepDuration
          ReminderInterval = reminderInterval
          MaxMessageCount = maxMessageCount
          Configuration = config }

type KillSwitch = { Token: CancellationToken }

[<RequireQualifiedAccess>]
module KillSwitch =
    let create () = { Token = CancellationToken(false) }

let ConfigureServices (settings: Settings) (services: IServiceCollection) =
    services
        .AddSingleton<Settings>(settings)
        .AddSingleton<KillSwitch>(KillSwitch.create ())
        .BuildServiceProvider()
