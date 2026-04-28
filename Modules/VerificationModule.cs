namespace LuduvoBot.Modules;

using System.Security.Cryptography;
using LuduvoBot.Data;
using LuduvoDotNet;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

public class VerificationModule:ApplicationCommandModule<ApplicationCommandContext>
{
    private static readonly TimeSpan DefaultTokenTtl=TimeSpan.FromMinutes(15);

    [SlashCommand("verifystart", "Start Luduvo verification by setting a bio token")]
    public async Task VerifyStartAsync(
        [SlashCommandParameter(Name = "username", Description = "Your Luduvo username")]string username)
    {
        try
        {
            var repository=CreateRepository();
            await repository.EnsureSchemaAsync();

            var verified=await repository.GetVerifiedByDiscordIdAsync(Context.User.Id);
            if (verified is not null)
            {
                await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
                {
                    Flags=MessageFlags.Ephemeral,
                    Embeds=
                    [
                        new EmbedProperties
                        {
                            Title="Already verified",
                            Description=$"Your Discord account is linked to **{verified.LuduvoUsername}** (ID `{verified.LuduvoUserId}`).",
                            Color=new Color(0x57F287),
                        },
                    ],
                }));
                return;
            }

            var token=CreateToken();
            var expiresAt=DateTimeOffset.UtcNow.Add(GetTokenTtl());
            await repository.UpsertPendingAsync(Context.User.Id, username, token, expiresAt);

            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Flags=MessageFlags.Ephemeral,
                Embeds=
                [
                    new EmbedProperties
                    {
                        Title="Verification started",
                        Description=$"Set your Luduvo bio to this value:\n`{token}`\nThen run `/verifycheck username:{username}` before `{expiresAt:yyyy-MM-dd HH:mm:ss} UTC`.",
                        Color=new Color(0x5865F2),
                    },
                ],
            }));
        }
        catch (InvalidOperationException ex)
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Flags=MessageFlags.Ephemeral,
                Embeds=
                [
                    new EmbedProperties
                    {
                        Title="Configuration error",
                        Description=ex.Message,
                        Color=new Color(0xED4245),
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
                        Description="An unexpected error occurred while starting verification.",
                        Color=new Color(0xED4245),
                    },
                ],
            }));
        }
    }

    [SlashCommand("verifycheck", "Finish Luduvo verification by checking your bio token")]
    public async Task VerifyCheckAsync(
        [SlashCommandParameter(Name = "username", Description = "Your Luduvo username")]string username)
    {
        try
        {
            var repository=CreateRepository();
            await repository.EnsureSchemaAsync();

            var pending=await repository.GetPendingAsync(Context.User.Id);
            if (pending is null)
            {
                await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
                {
                    Flags=MessageFlags.Ephemeral,
                    Embeds=
                    [
                        new EmbedProperties
                        {
                            Title="No pending verification",
                            Description="Run `/verifystart` first to get a bio token.",
                            Color=new Color(0xFEE75C),
                        },
                    ],
                }));
                return;
            }

            if (pending.ExpiresAt<=DateTimeOffset.UtcNow)
            {
                await repository.ClearPendingAsync(Context.User.Id);
                await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
                {
                    Flags=MessageFlags.Ephemeral,
                    Embeds=
                    [
                        new EmbedProperties
                        {
                            Title="Token expired",
                            Description="Your verification token expired. Run `/verifystart` again.",
                            Color=new Color(0xFEE75C),
                        },
                    ],
                }));
                return;
            }

            var user=await TryGetUserByUsernameAsync(username);
            if (user is null)
            {
                await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
                {
                    Flags=MessageFlags.Ephemeral,
                    Embeds=
                    [
                        new EmbedProperties
                        {
                            Title="User not found",
                            Description=$"No user found for username `{username}`.",
                            Color=new Color(0xED4245),
                        },
                    ],
                }));
                return;
            }

            var existing=await repository.GetVerifiedByLuduvoUserIdAsync(user.UserId);
            if (existing is not null && existing.DiscordUserId!=Context.User.Id)
            {
                await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
                {
                    Flags=MessageFlags.Ephemeral,
                    Embeds=
                    [
                        new EmbedProperties
                        {
                            Title="Already linked",
                            Description="That Luduvo account is already linked to another Discord user.",
                            Color=new Color(0xED4245),
                        },
                    ],
                }));
                return;
            }

            var bio=user.Bio?.Trim() ?? string.Empty;
            if (!bio.Contains(pending.Token, StringComparison.Ordinal))
            {
                await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
                {
                    Flags=MessageFlags.Ephemeral,
                    Embeds=
                    [
                        new EmbedProperties
                        {
                            Title="Bio does not match",
                            Description="Your Luduvo bio does not contain the verification token. Update your bio and try again.",
                            Color=new Color(0xFEE75C),
                        },
                    ],
                }));
                return;
            }

            await repository.MarkVerifiedAsync(Context.User.Id, user.UserId, user.Username);
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Flags=MessageFlags.Ephemeral,
                Embeds=
                [
                    new EmbedProperties
                    {
                        Title="Verified",
                        Description=$"Linked to **{user.Username}** (ID `{user.UserId}`).",
                        Color=new Color(0x57F287),
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
        catch (InvalidOperationException ex)
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Flags=MessageFlags.Ephemeral,
                Embeds=
                [
                    new EmbedProperties
                    {
                        Title="Configuration error",
                        Description=ex.Message,
                        Color=new Color(0xED4245),
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
                        Description="An unexpected error occurred while checking verification.",
                        Color=new Color(0xED4245),
                    },
                ],
            }));
        }
    }

    [SlashCommand("verifystatus", "Check your Luduvo verification status")]
    public async Task VerifyStatusAsync()
    {
        try
        {
            var repository=CreateRepository();
            await repository.EnsureSchemaAsync();

            var verified=await repository.GetVerifiedByDiscordIdAsync(Context.User.Id);
            if (verified is not null)
            {
                await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
                {
                    Flags=MessageFlags.Ephemeral,
                    Embeds=
                    [
                        new EmbedProperties
                        {
                            Title="Verified",
                            Description=$"Linked to **{verified.LuduvoUsername}** (ID `{verified.LuduvoUserId}`).",
                            Color=new Color(0x57F287),
                        },
                    ],
                }));
                return;
            }

            var pending=await repository.GetPendingAsync(Context.User.Id);
            if (pending is null)
            {
                await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
                {
                    Flags=MessageFlags.Ephemeral,
                    Embeds=
                    [
                        new EmbedProperties
                        {
                            Title="Not verified",
                            Description="No verification in progress. Run `/verifystart` to begin.",
                            Color=new Color(0xFEE75C),
                        },
                    ],
                }));
                return;
            }

            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Flags=MessageFlags.Ephemeral,
                Embeds=
                [
                    new EmbedProperties
                    {
                        Title="Verification pending",
                        Description=$"Token expires at `{pending.ExpiresAt:yyyy-MM-dd HH:mm:ss} UTC`. Run `/verifycheck username:{pending.LuduvoUsername}` once your bio is updated.",
                        Color=new Color(0xFEE75C),
                    },
                ],
            }));
        }
        catch (InvalidOperationException ex)
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Flags=MessageFlags.Ephemeral,
                Embeds=
                [
                    new EmbedProperties
                    {
                        Title="Configuration error",
                        Description=ex.Message,
                        Color=new Color(0xED4245),
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
                        Description="An unexpected error occurred while checking status.",
                        Color=new Color(0xED4245),
                    },
                ],
            }));
        }
    }

    [SlashCommand("verifyunlink", "Unlink your Luduvo account and clear pending verification")]
    public async Task VerifyUnlinkAsync()
    {
        try
        {
            var repository=CreateRepository();
            await repository.EnsureSchemaAsync();

            var verified=await repository.GetVerifiedByDiscordIdAsync(Context.User.Id);
            var pending=await repository.GetPendingAsync(Context.User.Id);
            if (verified is null && pending is null)
            {
                await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
                {
                    Flags=MessageFlags.Ephemeral,
                    Embeds=
                    [
                        new EmbedProperties
                        {
                            Title="Nothing to unlink",
                            Description="No linked account or pending verification was found.",
                            Color=new Color(0xFEE75C),
                        },
                    ],
                }));
                return;
            }

            await repository.UnlinkAsync(Context.User.Id);
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Flags=MessageFlags.Ephemeral,
                Embeds=
                [
                    new EmbedProperties
                    {
                        Title="Unlinked",
                        Description="Your Luduvo account link has been removed.",
                        Color=new Color(0x57F287),
                    },
                ],
            }));
        }
        catch (InvalidOperationException ex)
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Flags=MessageFlags.Ephemeral,
                Embeds=
                [
                    new EmbedProperties
                    {
                        Title="Configuration error",
                        Description=ex.Message,
                        Color=new Color(0xED4245),
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
                        Description="An unexpected error occurred while unlinking your account.",
                        Color=new Color(0xED4245),
                    },
                ],
            }));
        }
    }

    [SlashCommand("verifyprofile", "Show your linked Luduvo profile")]
    public async Task VerifyProfileAsync()
    {
        try
        {
            var repository=CreateRepository();
            await repository.EnsureSchemaAsync();

            var verified=await repository.GetVerifiedByDiscordIdAsync(Context.User.Id);
            if (verified is null)
            {
                await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
                {
                    Flags=MessageFlags.Ephemeral,
                    Embeds=
                    [
                        new EmbedProperties
                        {
                            Title="Not verified",
                            Description="You do not have a linked Luduvo account.",
                            Color=new Color(0xFEE75C),
                        },
                    ],
                }));
                return;
            }

            var user=await BaseModule.luduvo.GetUserByIdAsync(verified.LuduvoUserId);
            var message=await BuildUserMessageAsync(user);
            message.Flags=MessageFlags.Ephemeral;
            await RespondAsync(InteractionCallback.Message(message));
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
                        Description="The linked Luduvo account no longer exists.",
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
        catch (InvalidOperationException ex)
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Flags=MessageFlags.Ephemeral,
                Embeds=
                [
                    new EmbedProperties
                    {
                        Title="Configuration error",
                        Description=ex.Message,
                        Color=new Color(0xED4245),
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
                        Description="An unexpected error occurred while loading your profile.",
                        Color=new Color(0xED4245),
                    },
                ],
            }));
        }
    }

    [SlashCommand("verifylookup", "Moderator lookup of a member's linked Luduvo account", DefaultGuildPermissions = Permissions.ManageGuild)]
    public async Task VerifyLookupAsync(
        [SlashCommandParameter(Name = "member", Description = "Server member to look up")]GuildUser member)
    {
        try
        {
            var repository=CreateRepository();
            await repository.EnsureSchemaAsync();

            var verified=await repository.GetVerifiedByDiscordIdAsync(member.Id);
            if (verified is null)
            {
                await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
                {
                    Flags=MessageFlags.Ephemeral,
                    Embeds=
                    [
                        new EmbedProperties
                        {
                            Title="No linked account",
                            Description=$"No Luduvo account linked for <@{member.Id}>.",
                            Color=new Color(0xFEE75C),
                        },
                    ],
                }));
                return;
            }

            var user=await BaseModule.luduvo.GetUserByIdAsync(verified.LuduvoUserId);
            var message=await BuildUserMessageAsync(user);
            message.Flags=MessageFlags.Ephemeral;
            await RespondAsync(InteractionCallback.Message(message));
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
                        Description="The linked Luduvo account no longer exists.",
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
        catch (InvalidOperationException ex)
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Flags=MessageFlags.Ephemeral,
                Embeds=
                [
                    new EmbedProperties
                    {
                        Title="Configuration error",
                        Description=ex.Message,
                        Color=new Color(0xED4245),
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
                        Description="An unexpected error occurred while looking up the account.",
                        Color=new Color(0xED4245),
                    },
                ],
            }));
        }
    }

    private static VerificationRepository CreateRepository()
    {
        var connectionString=VerificationRepository.BuildConnectionStringFromEnvironment();
        return new VerificationRepository(connectionString);
    }

    private static TimeSpan GetTokenTtl()
    {
        var raw=Environment.GetEnvironmentVariable("LUDUVO_VERIFY_TOKEN_TTL_MINUTES");
        if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out var minutes) && minutes>0)
            return TimeSpan.FromMinutes(minutes);

        return DefaultTokenTtl;
    }

    private static string CreateToken()
    {
        var bytes=RandomNumberGenerator.GetBytes(6);
        return $"luduvo-verify-{Convert.ToHexString(bytes).ToLowerInvariant()}";
    }

    private static async Task<LuduvoDotNet.Records.User?> TryGetUserByUsernameAsync(string username)
    {
        var users=(await BaseModule.luduvo.SearchUsersAsync(username, 5, null)).ToList();
        if (!users.Any())
            return null;

        var match=users.FirstOrDefault(user => string.Equals(user.username, username, StringComparison.OrdinalIgnoreCase));
        var selected=match ?? users.FirstOrDefault();
        if (selected is null)
            return null;

        return await selected.GetUserAsync();
    }

    private static IEnumerable<EmbedFieldProperties> GetUserEmbed(LuduvoDotNet.Records.User user)
    {
        return
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
}