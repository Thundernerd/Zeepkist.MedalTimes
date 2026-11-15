using TNRD.Zeepkist.MedalTimes.Medals;
using ZeepSDK.Chat;
using ZeepSDK.ChatCommands;

namespace TNRD.Zeepkist.MedalTimes.Commands;

public sealed class ApexTimeChatCommand : ILocalChatCommand
{
    public string Prefix => "/";
    public string Command => "apex";
    public string Description => "Posts Apex time or availability as a chat message";
    public void Handle(string arguments) => ChatApi.SendMessage(
        Plugin.Drawer?.GetMessage(MedalTier.Apex) ?? "Apex time unavailable");
}
