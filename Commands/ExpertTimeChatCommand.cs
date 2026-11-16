using TNRD.Zeepkist.MedalTimes.Medals;
using ZeepSDK.Chat;
using ZeepSDK.ChatCommands;

namespace TNRD.Zeepkist.MedalTimes.Commands;

public sealed class ExpertTimeChatCommand : ILocalChatCommand
{
    public string Prefix => "/";
    public string Command => "expert";
    public string Description => "Posts Expert time or availability as a chat message";
    public void Handle(string arguments) => ChatApi.SendMessage(
        Plugin.Drawer?.GetMessage(MedalTier.Expert) ?? "Expert time unavailable");
}
