﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JsonAnything.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JsonAnything.Json
{
    public class JsonNodeConverter : JsonConverter<JsonNode>
    {
        public override void WriteJson(JsonWriter writer, JsonNode value, JsonSerializer serializer)
        {
            switch (value.Type)
            {
                case NodeType.Null:
                {
                    writer.WriteNull();
                    break;
                }
                case NodeType.Array:
                {
                    writer.WriteStartArray();

                    foreach (var elem in value.AsList)
                    {
                        WriteJson(writer, elem, serializer);
                    }

                    writer.WriteEndArray();

                    break;
                }
                case NodeType.Boolean:
                {
                    writer.WriteValue(value.AsBool);
                    break;
                }
                case NodeType.Number:
                {
                    writer.WriteValue(value.AsFloat);
                    break;
                }
                case NodeType.Integer:
                {
                    writer.WriteValue(value.AsInt);
                    break;
                }
                case NodeType.Object:
                {
                    writer.WriteStartObject();

                    foreach (KeyValuePair<string, JsonNode> kv in value.AsDictionary)
                    {
                        writer.WritePropertyName(kv.Key);
                        WriteJson(writer, kv.Value, serializer);
                    }

                    writer.WriteEndObject();

                    break;
                }
                case NodeType.String:
                {
                    writer.WriteValue(value.AsString);
                    break;
                }
            }
        }

        public override JsonNode ReadJson(JsonReader reader,
            Type objectType,
            JsonNode existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            JToken token = JToken.ReadFrom(reader);
            return Convert(token);
        }

        public JsonNode Convert(JToken j)
        {
            switch (j.Type)
            {
                case JTokenType.String:
                {
                    return new JsonNode(j.Value<string>(), NodeType.String, j);
                }
                case JTokenType.Boolean:
                {
                    return new JsonNode(j.Value<bool>(), NodeType.Boolean, j);
                }
                case JTokenType.Float:
                {
                    return new JsonNode(j.Value<float>(), NodeType.Number, j);
                }
                case JTokenType.Integer:
                {
                    return new JsonNode(j.Value<int>(), NodeType.Integer, j);
                }
                case JTokenType.Null:
                {
                    return new JsonNode(null, NodeType.Null, j);
                }
                case JTokenType.Array:
                {
                    List<JsonNode> arr = new List<JsonNode>();

                    int i = 0;
                    foreach (JToken t in j)
                    {
                        JsonNode arrElement = Convert(t);
                        arrElement.Key = i.ToString();
                        arr.Add(arrElement);

                        i++;
                    }

                    return new JsonNode(arr, NodeType.Array, j);
                }
                case JTokenType.Object:
                {
                    Dictionary<string, JsonNode> obj = new Dictionary<string, JsonNode>();

                    foreach (JProperty p in j.Children<JProperty>())
                    {
                        obj[p.Name] = Convert(p.Value);
                        obj[p.Name].Key = p.Name;
                    }

                    return new JsonNode(obj, NodeType.Object, j);
                }
                default:
                {
                    Logger.Log()(LogLevel.WARN, "Unknown Token type {0}", j.Type.ToString());
                    return null;
                }
            }
        }
    }
}
