namespace LuduvoBot.Modules;

using LuduvoDotNet;
using LuduvoDotNet.Records;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

public class PlacesModule:ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("getplacebyid", "Get a place from ID")]
    public async Task GetPlaceByIdAsync(
        [SlashCommandParameter(Name = "id", Description = "The id of the place")]uint id)
    {
        try
        {
            var place=await BaseModule.luduvo.GetPlaceByIdAsync(id);
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Embeds=[BuildPlaceEmbed(place)],
            }));
        }
        catch (PlaceNotFoundException)
        {
            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Flags=MessageFlags.Ephemeral,
                Embeds=
                [
                    new EmbedProperties
                    {
                        Title="Place not found",
                        Description=$"No place found for ID `{id}`.",
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
                        Description="An unexpected error occurred while fetching place data.",
                        Color=new Color(0xED4245),
                    },
                ],
            }));
        }
    }

    [SlashCommand("getplace", "Get a place from name")]
    public async Task GetPlaceByNameAsync(
        [SlashCommandParameter(Name = "name", Description = "The name of the place")]string name)
    {
        try
        {
            var places=await BaseModule.luduvo.SearchPlacesAsync(name, 1);
            var place=places.FirstOrDefault();
            if (place is null)
            {
                await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
                {
                    Flags=MessageFlags.Ephemeral,
                    Embeds=
                    [
                        new EmbedProperties
                        {
                            Title="Place not found",
                            Description=$"No place found for name `{name}`.",
                            Color=new Color(0xFEE75C),
                        },
                    ],
                }));
                return;
            }

            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Embeds=[BuildPlaceEmbed(place)],
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
                        Description="An unexpected error occurred while fetching place data.",
                        Color=new Color(0xED4245),
                    },
                ],
            }));
        }
    }

    [SlashCommand("searchplaces", "Search places by name")]
    public async Task SearchPlacesAsync(
        [SlashCommandParameter(Name = "query", Description = "Text used to find places")]string query)
    {
        try
        {
            var places=(await BaseModule.luduvo.SearchPlacesAsync(query, 5)).ToList();
            if (!places.Any())
            {
                await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
                {
                    Flags=MessageFlags.Ephemeral,
                    Embeds=
                    [
                        new EmbedProperties
                        {
                            Title="No results",
                            Description=$"No places found for `{query}`.",
                            Color=new Color(0xFEE75C),
                        },
                    ],
                }));
                return;
            }

            var lines=places.Select((place, index) =>
                $"`{index + 1}.` **{place.Title}** (`{place.Id}`) - {place.ActivePlayers}/{place.MaxPlayers} players");

            await RespondAsync(InteractionCallback.Message(new InteractionMessageProperties
            {
                Embeds=
                [
                    new EmbedProperties
                    {
                        Title=$"Search results for '{query}'",
                        Description=string.Join('\n', lines),
                        Color=new Color(0x5865F2),
                        Timestamp=DateTimeOffset.UtcNow,
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
                        Description="An unexpected error occurred while searching places.",
                        Color=new Color(0xED4245),
                    },
                ],
            }));
        }
    }

    private static EmbedProperties BuildPlaceEmbed(Place place)
    {
        var embed=new EmbedProperties
        {
            Title=string.IsNullOrWhiteSpace(place.Title) ? $"Place #{place.Id}" : place.Title,
            Description=string.IsNullOrWhiteSpace(place.Description) ? "No description provided." : place.Description,
            Color=new Color(0x5865F2),
            Fields=
            [
                new EmbedFieldProperties
                {
                    Name="ID",
                    Value=place.Id.ToString(),
                    Inline=true,
                },
                new EmbedFieldProperties
                {
                    Name="Owner",
                    Value=string.IsNullOrWhiteSpace(place.OwnerUsername) ? "-" : place.OwnerUsername,
                    Inline=true,
                },
                new EmbedFieldProperties
                {
                    Name="Players",
                    Value=$"{place.ActivePlayers}/{place.MaxPlayers}",
                    Inline=true,
                },
                new EmbedFieldProperties
                {
                    Name="Visits",
                    Value=place.VisitCount.ToString(),
                    Inline=true,
                },
                new EmbedFieldProperties
                {
                    Name="Likes / Dislikes",
                    Value=$"{place.ThumbsUp} / {place.ThumbsDown}",
                    Inline=true,
                },
                new EmbedFieldProperties
                {
                    Name="Created",
                    Value=$"{place.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC",
                    Inline=true,
                },
                new EmbedFieldProperties
                {
                    Name="Updated",
                    Value=$"{place.UpdatedAt:yyyy-MM-dd HH:mm:ss} UTC",
                    Inline=true,
                },
            ],
            Timestamp=DateTimeOffset.UtcNow,
        };

        if (place.ThumbnailUrl is not null)
            embed.Thumbnail=new EmbedThumbnailProperties(place.ThumbnailUrl.ToString());

        return embed;
    }
}