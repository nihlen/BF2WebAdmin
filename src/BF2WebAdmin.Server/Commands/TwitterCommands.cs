using BF2WebAdmin.Common;
using BF2WebAdmin.Server.Abstractions;
using BF2WebAdmin.Server.Extensions;

namespace BF2WebAdmin.Server.Commands
{
    [Command("follow <Stream>", Auth.All)]
    public class TwitterFollowCommand : BaseCommand
    {
        public string Stream { get; set; }
    }

    [Command("unfollow", Auth.All)]
    public class TwitterUnfollowCommand : BaseCommand
    {
        
    }

}