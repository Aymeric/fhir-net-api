﻿using Hl7.Fhir.Support;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hl7.Fhir.Serialization
{
    public class JsonDomFhirReader : IFhirReader
    {
        private JToken _current;

        internal JToken Current { get { return _current; } }            // just for while refactoring

        internal JsonDomFhirReader(JToken root)
        {
            _current = root;
        }

        public JsonDomFhirReader(JsonReader reader)
        {
            reader.DateParseHandling = DateParseHandling.None;
            reader.FloatParseHandling = FloatParseHandling.Decimal;

            _current = JObject.Load(reader);
        }

        public TokenType CurrentToken
        {
            get
            {
                if (_current is JObject) return TokenType.Object;
                if (_current is JArray) return TokenType.Array;
                if (_current is JValue)
                {
                    var val = (JValue)_current;
                    if(val.Type == JTokenType.Integer || val.Type == JTokenType.Float) return TokenType.Number;
                    if(val.Type == JTokenType.Boolean) return TokenType.Boolean;
                    if(val.Type == JTokenType.String) return TokenType.String;
                    if (val.Type == JTokenType.Null) return TokenType.Null;

                    throw Error.Format("Encountered a json primitive of type {0} while only string, boolean and number are allowed", val.Type);
                }

                throw Error.Format("Json reader encountered a token of type {0}, which is not supported in the Fhir json serialization", _current.GetType().Name);
            }
        }

        public object GetPrimitiveValue()
        {
            if (_current is JValue)
                return ((JValue)_current).Value;
            else
                throw Error.Format("Tried to read a primitive value while reader is not at a json primitive");
        }

        public string GetResourceTypeName(bool nested)
        {
            if (CurrentToken != TokenType.Object)
                throw Error.Format("Need to be at a complex object to determine resource type");

            var resourceTypeMember = ((JObject)_current)[SerializationConfig.RESOURCETYPE_MEMBER_NAME];

            if (resourceTypeMember != null)
            {
                if (resourceTypeMember is JValue)
                {
                    var memberValue = (JValue)resourceTypeMember;

                    if (memberValue.Type == JTokenType.String)
                    {
                        return (string)memberValue.Value;
                    }
                }

                throw Error.Format("resourceType should be a primitive string json value");
            }

            throw Error.Format("Cannot determine type of resource to create from json input data: no member {0} was found", 
                            SerializationConfig.RESOURCETYPE_MEMBER_NAME);
        }

        //When enumerating properties for a complex object, make sure not to let resourceType get through
        //TODO: Detecting whether this is a special, serialization format-specific member should be
        //done in an abstraction around the json or xml readers.


        public IEnumerable<Tuple<string, IFhirReader>> GetMembers()
        {
            var complex = _current as JObject;
 
            if (complex == null)
                throw Error.Format("Need to be at a complex object to list child members");
           
            foreach(var member in complex)
            {
                var memberName = member.Key;

                if(memberName != SerializationConfig.RESOURCETYPE_MEMBER_NAME)
                {
                    IFhirReader nestedReader = new JsonDomFhirReader(member.Value);

                    // Map contents of _membername elements to the normal 'membername'
                    // effectively treating this as if an objects properies are spread out
                    // over two separate json objects
                    if (memberName.StartsWith("_")) memberName = memberName.Remove(0, 1);

                    yield return Tuple.Create(memberName,nestedReader);
                }
            }
        }

        public IEnumerable<IFhirReader> GetArrayElements()
        {
            var array = _current as JArray;

            if (array == null)
                throw Error.Format("Need to be at an array to list elements");

            foreach(var element in array)
            {
                yield return new JsonDomFhirReader(element);
            }
        }

        public int LineNumber
        {
            get
            {
                var li = (IJsonLineInfo)_current;

                if (!li.HasLineInfo())
                    throw Error.InvalidOperation("No lineinfo available. Please read the Json document using...");

                return li.LineNumber;
            }
        }

        public int LinePosition
        {
            get
            {
                var li = (IJsonLineInfo)_current;

                if (!li.HasLineInfo())
                    throw Error.InvalidOperation("No lineinfo available. Please read the Json document using...");

                return li.LinePosition;
            }
        }
    }
}
