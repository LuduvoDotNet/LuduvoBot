namespace LuduvoBot.Modules;
using LuduvoDotNet;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

public class UserModule:ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("getuserbyid", "Get a user from ID")]
    public async Task GetUserByIdAsync(
        [SlashCommandParameter(Name = "id",Description = "The id of the user")]uint id)
    {
        try
        {
            var user=await BaseModule.luduvo.GetUserByIdAsync(id);

            var embed=new EmbedProperties
            {
                Title=user.Username,
                Description=string.IsNullOrWhiteSpace(user.Bio) ? "No bio provided." : user.Bio,
                Color=new Color(0x5865F2),
                Fields=
                [
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
                ],
                Timestamp=DateTimeOffset.UtcNow,
            };

            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Embeds=[embed],
            }));
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
            var user = await users.FirstOrDefault().GetUserAsync();
            var embed=new EmbedProperties
            {
                Title=user.Username,
                Description=string.IsNullOrWhiteSpace(user.Bio) ? "No bio provided." : user.Bio,
                Color=new Color(0x5865F2),
                Fields=
                [
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
                ],
                Timestamp=DateTimeOffset.UtcNow,
            };

            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Embeds=[embed],
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
}