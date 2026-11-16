using TNRD.Zeepkist.MedalTimes.Medals;
using ZeepSDK.Chat;
using ZeepSDK.ChatCommands;

namespace TNRD.Zeepkist.MedalTimes.Commands;

public sealed class AuthorTimeChatCommand : ILocalChatCommand
{
    public string Prefix => "/";
    public string Command => "at";
    public string Description => "Posts Author time as a chat message";
    public void Handle(string arguments) => ChatApi.SendMessage(
        Plugin.Drawer?.GetMessage(MedalTier.Author) ?? "Author time unavailable");
}
