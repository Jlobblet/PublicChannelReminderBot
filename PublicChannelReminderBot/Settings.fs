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
    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | DiscordToken _ -> ""


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

        { DiscordToken = results.GetResult <@ DiscordToken @>
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
