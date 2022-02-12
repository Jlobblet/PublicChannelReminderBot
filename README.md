# PublicChannelReminderBot

A bot to (gently) remind you to use a public channel over a private one.

## Installation

Require the [.NET 6.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/6.0).

## Configuration

There are two files to configure:
- Copy `appsettings.example.json` to `appsettings.json` and specify a Discord token.
- Copy `channels.example.json` to `channels.json`, then add entries to it.
  The key is the guild (Discord server) id, and the values are the channel ids within those servers.

## Running

`dotnet run -c Release`
