using Siemens.Engineering;
using Siemens.Engineering.SW.Blocks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace TiaMcpServer.ModelContextProtocol
{
    public class Helper
    {
        public static List<Attribute> GetAttributeList(IEngineeringObject obj)
        {
            var attributes = new List<Attribute>();

            if (obj != null)
            {
                foreach (var attr in obj.GetAttributeInfos())
                {
                    object? value;

                    try
                    {
                        value = ToJsonSafe(obj.GetAttribute(attr.Name));
                    }
                    catch (Exception ex)
                    {
                        // One attribute Openness refuses to read must not cost the caller all
                        // the others.
                        value = $"<unreadable: {ex.Message}>";
                    }

                    attributes.Add(new Attribute
                    {
                        Name = attr.Name,
                        Value = value,
                        AccessMode = Enum.GetName(typeof(EngineeringAttributeAccessMode), attr.AccessMode)
                    });
                }
            }

            return attributes;
        }

        /// <summary>
        /// Reduces an Openness attribute value to something System.Text.Json can write.
        /// Attribute values are typed 'object' and include FileInfo, DirectoryInfo, CultureInfo
        /// and live engineering objects (a project's Path, a device's parent). Serializing those
        /// throws after the tool method has returned, outside its try/catch, which the client
        /// saw as "An error occurred invoking 'get_project'" with no reason.
        /// </summary>
        public static object? ToJsonSafe(object? value, int depth = 0)
        {
            switch (value)
            {
                case null:
                    return null;

                case string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or decimal:
                    return value;

                // JSON has no NaN or Infinity; the serializer throws on them.
                case double d:
                    return double.IsNaN(d) || double.IsInfinity(d) ? d.ToString(CultureInfo.InvariantCulture) : value;

                case float f:
                    return float.IsNaN(f) || float.IsInfinity(f) ? f.ToString(CultureInfo.InvariantCulture) : value;

                case Enum e:
                    return e.ToString();

                case DateTime dateTime:
                    return dateTime.ToString("o", CultureInfo.InvariantCulture);

                case DateTimeOffset dateTimeOffset:
                    return dateTimeOffset.ToString("o", CultureInfo.InvariantCulture);

                case FileSystemInfo fileSystemInfo:
                    return fileSystemInfo.FullName;

                case CultureInfo cultureInfo:
                    return cultureInfo.Name;

                case MultilingualText multilingualText:
                    return FirstText(multilingualText);

                case IEngineeringObject engineeringObject:
                    return NameOf(engineeringObject);

                case IEnumerable sequence:
                    if (depth >= MaxNesting)
                    {
                        return value.ToString();
                    }

                    var items = new List<object?>();

                    foreach (var item in sequence)
                    {
                        if (items.Count >= MaxItems)
                        {
                            items.Add("...");

                            break;
                        }

                        items.Add(ToJsonSafe(item, depth + 1));
                    }

                    return items;

                default:
                    return value.ToString();
            }
        }

        private const int MaxNesting = 2;

        private const int MaxItems = 200;

        private static string? NameOf(IEngineeringObject obj)
        {
            try
            {
                if (obj.GetType().GetProperty("Name")?.GetValue(obj) is string name && name.Length > 0)
                {
                    return name;
                }
            }
            catch (Exception)
            {
                // Fall through to ToString(): a name is a nicety, not a requirement.
            }

            return obj.ToString();
        }

        /// <summary>
        /// First available translation of a MultilingualText (tag and constant comments), or
        /// null when the text has no items. Openness returns one item per project language.
        /// </summary>
        public static string? FirstText(MultilingualText? text)
        {
            if (text == null)
            {
                return null;
            }

            foreach (var item in text.Items)
            {
                return item.Text;
            }

            return null;
        }

        public static BlockGroupInfo BuildBlockHierarchy(PlcBlockGroup group, TiaMcpServer.Siemens.Portal? portal = null)
        {
            var groupInfo = new BlockGroupInfo
            {
                Name = group.Name
            };

            var blockList = new List<ResponseBlockInfo>();
            foreach (var block in group.Blocks)
            {
                var attributes = Helper.GetAttributeList(block);
                blockList.Add(new ResponseBlockInfo
                {
                    Path = portal == null ? null : portal.GetBlockPath(block),
                    Name = block.Name,
                    TypeName = block.GetType().Name,
                    Namespace = block.Namespace,
                    ProgrammingLanguage = Enum.GetName(typeof(ProgrammingLanguage), block.ProgrammingLanguage),
                    MemoryLayout = Enum.GetName(typeof(MemoryLayout), block.MemoryLayout),
                    IsConsistent = block.IsConsistent,
                    HeaderName = block.HeaderName,
                    ModifiedDate = block.ModifiedDate,
                    IsKnowHowProtected = block.IsKnowHowProtected,
                    Attributes = attributes,
                    Description = block.ToString()
                });
            }
            groupInfo.Blocks = blockList;

            var groupList = new List<BlockGroupInfo>();
            foreach (var subGroup in group.Groups)
            {
                groupList.Add(BuildBlockHierarchy(subGroup, portal));
            }
            groupInfo.Groups = groupList;

            return groupInfo;
        }
    }
}
