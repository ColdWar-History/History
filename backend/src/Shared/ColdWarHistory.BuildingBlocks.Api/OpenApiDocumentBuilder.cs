using System.Text.Json.Nodes;

namespace ColdWarHistory.BuildingBlocks.Api;

public static class OpenApiDocumentBuilder
{
    public static JsonObject Build(string title, string version, IEnumerable<OpenApiEndpointDescription> endpoints)
    {
        var paths = new JsonObject();

        foreach (var endpoint in endpoints)
        {
            if (!paths.TryGetPropertyValue(endpoint.Path, out var existingNode) || existingNode is not JsonObject pathItem)
            {
                pathItem = new JsonObject();
                paths[endpoint.Path] = pathItem;
            }

            var operation = new JsonObject
            {
                ["summary"] = endpoint.Summary,
                ["tags"] = new JsonArray(endpoint.Tag),
                ["responses"] = BuildResponses(endpoint.Responses)
            };

            if (endpoint.RequestFields.Count > 0)
            {
                operation["requestBody"] = new JsonObject
                {
                    ["required"] = true,
                    ["content"] = new JsonObject
                    {
                        ["application/json"] = new JsonObject
                        {
                            ["schema"] = BuildSchema(endpoint.RequestFields)
                        }
                    }
                };
            }

            if (endpoint.RequiresAuthentication)
            {
                operation["security"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["bearerAuth"] = new JsonArray()
                    }
                };
            }

            pathItem[endpoint.Method.ToLowerInvariant()] = operation;
        }

        return new JsonObject
        {
            ["openapi"] = "3.0.1",
            ["info"] = new JsonObject
            {
                ["title"] = title,
                ["version"] = version
            },
            ["paths"] = paths,
            ["components"] = new JsonObject
            {
                ["securitySchemes"] = new JsonObject
                {
                    ["bearerAuth"] = new JsonObject
                    {
                        ["type"] = "http",
                        ["scheme"] = "bearer"
                    }
                }
            }
        };
    }

    private static JsonObject BuildResponses(IEnumerable<OpenApiResponseDescription> responses)
    {
        var result = new JsonObject();

        foreach (var response in responses)
        {
            result[response.StatusCode.ToString()] = new JsonObject
            {
                ["description"] = response.Description
            };
        }

        return result;
    }

    private static JsonObject BuildSchema(IEnumerable<OpenApiField> fields)
    {
        var properties = new JsonObject();
        var required = new JsonArray();

        foreach (var field in fields)
        {
            properties[field.Name] = new JsonObject
            {
                ["type"] = field.Type,
                ["description"] = field.Description
            };

            if (field.Required)
            {
                required.Add(field.Name);
            }
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required
        };
    }
}
