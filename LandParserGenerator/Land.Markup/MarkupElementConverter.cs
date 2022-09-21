using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Land.Markup
{
	public class MarkupElementConverter: JsonConverter
	{
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(MarkupElement);
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			var jo = Newtonsoft.Json.Linq.JObject.Load(reader);

			if (jo.TryGetValue("Elements", out Newtonsoft.Json.Linq.JToken value))
			{
				return jo.ToObject<Concern>(serializer);
			}
			else
			{
				return jo.ToObject<ConcernPoint>(serializer);
			}
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			serializer.Serialize(writer, value);
		}
	}
}
