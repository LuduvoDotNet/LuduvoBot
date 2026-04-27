namespace LuduvoBot.Modules;
using LuduvoDotNet;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

public class UserModule:ApplicationCommandModule<ApplicationCommandContext>
{
    private IEnumerable<EmbedFieldProperties> GetUserEmbed(LuduvoDotNet.Records.User user)
    {
        return [
            new EmbedFieldProperties
            {
                Name="Display name",
                Value=string.IsNullOrWhiteSpace(user.DisplayName) ? "-" : user.DisplayName,
                Inline=true,
            },
            new EmbedFieldProperties
            {
                Name="ID",
                Value=user.UserId.ToString(),
                Inline=true,
            },
            new EmbedFieldProperties
            {
                Name="Status",
                Value=string.IsNullOrWhiteSpace(user.Status) ? "-" : user.Status,
                Inline=true,
            },
            new EmbedFieldProperties
            {
                Name="Friends / Places / Items",
                Value=$"{user.FriendCount} / {user.PlaceCount} / {user.ItemCount}",
                Inline=false,
            },
            new EmbedFieldProperties
            {
                Name="Member since",
                Value=$"{user.MemberSince:yyyy-MM-dd HH:mm:ss} UTC",
                Inline=false,
            },
        ];
    }
    private async Task<(AttachmentProperties Attachment, string FileName)?> TryCreateHeadshotAttachmentAsync(uint userId)
    {
        if (userId>int.MaxValue)
            return null;

        try
        {
            var bytes=await BaseModule.luduvo.GetUserHeadshot((int)userId);
            if (bytes.Length==0)
                return null;

            var fileName=$"headshot-{userId}.png";
            var stream=new MemoryStream(bytes, writable:false);
            return (new AttachmentProperties(fileName, stream), fileName);
        }
        catch (UserNotFoundException)
        {
            return null;
        }
        catch (TooManyRequestsException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetBannerUrl(LuduvoDotNet.Records.User user)
    {
        var bannerUrl=user.BannerUrl;
        if (bannerUrl is null)
            return null;

        if (bannerUrl.IsAbsoluteUri&&(bannerUrl.Scheme==Uri.UriSchemeHttps||bannerUrl.Scheme==Uri.UriSchemeHttp))
            return bannerUrl.ToString();

        return null;
    }

    private async Task<InteractionMessageProperties> BuildUserMessageAsync(LuduvoDotNet.Records.User user)
    {
        var embed=new EmbedProperties
        {
            Title=user.Username,
            Description=string.IsNullOrWhiteSpace(user.Bio) ? "No bio provided." : user.Bio,
            Color=new Color(0x5865F2),
            Fields=GetUserEmbed(user),
            Timestamp=DateTimeOffset.UtcNow,
        };

        var bannerUrl=TryGetBannerUrl(user);
        if (!string.IsNullOrWhiteSpace(bannerUrl))
            embed.Image=new EmbedImageProperties(bannerUrl);

        var headshot=await TryCreateHeadshotAttachmentAsync(user.UserId);
        if (headshot is null)
            return new InteractionMessageProperties { Embeds=[embed] };

        embed.Thumbnail=new EmbedThumbnailProperties($"attachment://{headshot.Value.FileName}");
        return new InteractionMessageProperties
        {
            Embeds=[embed],
            Attachments=[headshot.Value.Attachment],
        };
    }

    [SlashCommand("getuserbyid", "Get a user from ID")]
    public async Task GetUserByIdAsync(
        [SlashCommandParameter(Name = "id",Description = "The id of the user")]uint id)
    {
        try
        {
            var user=await BaseModule.luduvo.GetUserByIdAsync(id);
            await RespondAsync(InteractionCallback.Message(await BuildUserMessageAsync(user)));
        }
        catch (UserNotFoundException)
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Flags=MessageFlags.Ephemeral,
                Embeds=
                [
                    new EmbedProperties
                    {
                        Title="User not found",
                        Description=$"No user found for ID `{id}`.",
                        Color=new Color(0xED4245),
                    },
                ],
            }));
        }
        catch (TooManyRequestsException)
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Flags=MessageFlags.Ephemeral,
                Embeds=
                [
                    new EmbedProperties
                    {
                        Title="Rate limited",
                        Description="The API is rate limiting requests right now. Please try again in a moment.",
                        Color=new Color(0xFEE75C),
                    },
                ],
            }));
        }
        catch (Exception)
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Flags=MessageFlags.Ephemeral,
                Embeds=
                [
                    new EmbedProperties
                    {
                        Title="Unexpected error",
                        Description="An unexpected error occurred while fetching user data.",
                        Color=new Color(0xED4245),
                    },
                ],
            }));
        }
    }
    [SlashCommand("getuser","Gets user from username")]
    public async Task GetUserByUsernameAsync(
        [SlashCommandParameter(Name = "username",Description = "the username of the user")]string username)
    {
        try
        {
            var users=await BaseModule.luduvo.SearchUsersAsync(username,1,null);
            if (!users.Any())
            {
                await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
                    {
                        Embeds =
                        [
                            new EmbedProperties
                            {
                                Title = "Not found",
                                Description = $"No user found for username `{username}`.",
                                Color = new Color(0xFEE75C),
                            },
                        ]
                    }
                ));
                return;
            }
            var firstUser = users.FirstOrDefault();
            if (firstUser is null)
            {
                await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
                    {
                        Flags = MessageFlags.Ephemeral,
                        Embeds =
                        [
                            new EmbedProperties
                            {
                                Title = "Not found",
                                Description = $"No user found for username `{username}`.",
                                Color = new Color(0xFEE75C),
                            },
                        ]
                    }
                ));
                return;
            }

            var user = await firstUser.GetUserAsync();
            await RespondAsync(InteractionCallback.Message(await BuildUserMessageAsync(user)));
        }
        catch (TooManyRequestsException)
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Flags=MessageFlags.Ephemeral,
                Embeds=
                [
                    new EmbedProperties
                    {
                        Title="Rate limited",
                        Description="The API is rate limiting requests right now. Please try again in a moment.",
                        Color=new Color(0xFEE75C),
                    },
                ],
            }));
        }
        catch (Exception)
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Flags=MessageFlags.Ephemeral,
                Embeds=
                [
                    new EmbedProperties
                    {
                        Title="Unexpected error",
                        Description="An unexpected error occurred while fetching user data.",
                        Color=new Color(0xED4245),
                    },
                ],
            }));
        }
    } 
    
    [SlashCommand("getlatestuser","Gets the latest registered user")]
    public async Task GetLatestUserAsync()
    {
        try
        {
            var users = await BaseModule.luduvo.SearchUsersAsync(string.Empty, 1, null);
            var latestUser = users.FirstOrDefault();

            if (latestUser is null)
            {
                await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
                {
                    Flags = MessageFlags.Ephemeral,
                    Embeds =
                    [
                        new EmbedProperties
                        {
                            Title = "No users found",
                            Description = "Could not retrieve the latest registered user at this moment.",
                            Color = new Color(0xFEE75C),
                        },
                    ],
                }));
                return;
            }

            var user = await latestUser.GetUserAsync();
            await RespondAsync(InteractionCallback.Message(await BuildUserMessageAsync(user)));
        }
        catch (TooManyRequestsException)
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Flags = MessageFlags.Ephemeral,
                Embeds =
                [
                    new EmbedProperties
                    {
                        Title = "Rate limited",
                        Description = "The API is rate limiting requests right now. Please try again in a moment.",
                        Color = new Color(0xFEE75C),
                    },
                ],
            }));
        }
        catch (Exception)
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Flags = MessageFlags.Ephemeral,
                Embeds =
                [
                    new EmbedProperties
                    {
                        Title = "Unexpected error",
                        Description = "An unexpected error occurred while fetching user data.",
                        Color = new Color(0xED4245),
                    },
                ],
            }));
        }
    }
}