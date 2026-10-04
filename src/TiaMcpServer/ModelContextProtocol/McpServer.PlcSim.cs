using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ModelContextProtocol.Server;
using Siemens.Simatic.Simulation.Runtime;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpServer
    {
        // SimulationRuntimeManager is entirely static

        [McpServerTool(Name = "plcsim_batch_read"), Description("Read multiple simulation tags in a single operation")]
        public static string PlcSimBatchRead() { return "Not implemented"; }

        [McpServerTool(Name = "plcsim_batch_write"), Description("Write multiple simulation tags in a single operation")]
        public static string PlcSimBatchWrite() { return "Not implemented"; }

        [McpServerTool(Name = "plcsim_connect"), Description("Connect to a PLCSim simulation instance")]
        public static string PlcSimConnect()
        {
            return $"Connected to PLCSIM Advanced API. Version: {SimulationRuntimeManager.Version}";
        }

        [McpServerTool(Name = "plcsim_create_instance"), Description("Create a new PLCSim virtual controller instance")]
        public static string PlcSimCreateInstance(
            [Description("Name of the instance")] string instanceName,
            [Description("CPU type (e.g. CPU1500_Unspecified)")] string cpuType = "CPU1500_Unspecified")
        {
            if (Enum.TryParse<ECPUType>(cpuType, out var eCpuType))
            {
                var instance = SimulationRuntimeManager.RegisterInstance(eCpuType, instanceName);
                return $"Instance '{instanceName}' created successfully.";
            }
            return $"Failed: Invalid CPU type '{cpuType}'.";
        }

        [McpServerTool(Name = "plcsim_delete_instance"), Description("Delete an existing PLCSim instance")]
        public static string PlcSimDeleteInstance([Description("Name of the instance")] string instanceName)
        {
            var instances = SimulationRuntimeManager.RegisteredInstanceInfo;
            bool found = false;
            foreach (var i in instances) {
                if (i.Name == instanceName) { found = true; break; }
            }
            if (found)
            {
                SimulationRuntimeManager.CreateInterface(instanceName).UnregisterInstance();
                return $"Instance '{instanceName}' deleted.";
            }
            return $"Instance '{instanceName}' not found.";
        }

        [McpServerTool(Name = "plcsim_delete_profile"), Description("Delete a saved PLCSim simulation profile")]
        public static string PlcSimDeleteProfile() { return "Not implemented"; }

        [McpServerTool(Name = "plcsim_disconnect"), Description("Disconnect from the PLCSim simulation")]
        public static string PlcSimDisconnect() { return "Not implemented"; }

        [McpServerTool(Name = "plcsim_get_instance_config"), Description("Get configuration of a PLCSim instance")]
        public static string PlcSimGetInstanceConfig() { return "Not implemented"; }

        [McpServerTool(Name = "plcsim_get_instance_state"), Description("Get the current state of a PLCSim instance")]
        public static string PlcSimGetInstanceState([Description("Name of the instance")] string instanceName)
        {
            var instance = SimulationRuntimeManager.CreateInterface(instanceName);
            if (instance != null)
            {
                return $"State of '{instanceName}': {instance.OperatingState}";
            }
            return $"Instance '{instanceName}' not found.";
        }

        [McpServerTool(Name = "plcsim_get_network"), Description("Get network settings of the simulation")]
        public static string PlcSimGetNetwork() { return "Not implemented"; }

        [McpServerTool(Name = "plcsim_get_runtime_config"), Description("Get runtime configuration of the simulation")]
        public static string PlcSimGetRuntimeConfig() { return "Not implemented"; }

        [McpServerTool(Name = "plcsim_get_simulation_state"), Description("Get the overall simulation state")]
        public static string PlcSimGetSimulationState() { return "Not implemented"; }

        [McpServerTool(Name = "plcsim_list_instances"), Description("List all available PLCSim instances")]
        public static string PlcSimListInstances()
        {
            var instances = SimulationRuntimeManager.RegisteredInstanceInfo;
            if (instances == null || instances.Length == 0) return "No instances found.";
            return string.Join("\n", instances.Select(i => $"- {i.Name}"));
        }

        [McpServerTool(Name = "plcsim_list_profiles"), Description("List all saved simulation profiles")]
        public static string PlcSimListProfiles() { return "Not implemented"; }

        [McpServerTool(Name = "plcsim_list_tags"), Description("List all tags available in the simulation")]
        public static string PlcSimListTags(
            [Description("Name of the instance")] string instanceName,
            [Description("Maximum tags to return")] int limit = 100)
        {
            var instance = SimulationRuntimeManager.CreateInterface(instanceName);
            if (instance != null)
            {
                instance.UpdateTagList();
                var tags = instance.TagInfos;
                if (tags == null || tags.Length == 0) return "No tags found.";
                return string.Join("\n", tags.Take(limit).Select(t => $"- {t.Name} ({t.PrimitiveDataType})"));
            }
            return $"Instance '{instanceName}' not found.";
        }

        [McpServerTool(Name = "plcsim_load_profile"), Description("Load a saved simulation profile")]
        public static string PlcSimLoadProfile(
            [Description("Name of the instance")] string instanceName,
            [Description("Path to the storage folder")] string storagePath)
        {
            var instance = SimulationRuntimeManager.CreateInterface(instanceName);
            if (instance != null)
            {
                instance.RetrieveStorage(storagePath);
                return $"Profile loaded for '{instanceName}' from '{storagePath}'.";
            }
            return $"Instance '{instanceName}' not found.";
        }

        [McpServerTool(Name = "plcsim_memory_reset"), Description("Reset the memory of the simulated PLC")]
        public static string PlcSimMemoryReset([Description("Name of the instance")] string instanceName)
        {
            var instance = SimulationRuntimeManager.CreateInterface(instanceName);
            if (instance != null)
            {
                instance.MemoryReset();
                return $"Memory reset triggered for '{instanceName}'.";
            }
            return $"Instance '{instanceName}' not found.";
        }

        [McpServerTool(Name = "plcsim_power_off"), Description("Power off the simulated PLC")]
        public static string PlcSimPowerOff([Description("Name of the instance")] string instanceName)
        {
            var instance = SimulationRuntimeManager.CreateInterface(instanceName);
            if (instance != null)
            {
                instance.PowerOff();
                return $"Power off triggered for '{instanceName}'.";
            }
            return $"Instance '{instanceName}' not found.";
        }

        [McpServerTool(Name = "plcsim_power_on"), Description("Power on the simulated PLC")]
        public static string PlcSimPowerOn([Description("Name of the instance")] string instanceName)
        {
            var instance = SimulationRuntimeManager.CreateInterface(instanceName);
            if (instance != null)
            {
                instance.PowerOn();
                return $"Power on triggered for '{instanceName}'.";
            }
            return $"Instance '{instanceName}' not found.";
        }

        [McpServerTool(Name = "plcsim_read_tag"), Description("Read the value of a single simulation tag")]
        public static string PlcSimReadTag(
            [Description("Name of the instance")] string instanceName,
            [Description("Tag name (e.g. \"=201+?-QF1:11\")")] string tagName)
        {
            var instance = SimulationRuntimeManager.CreateInterface(instanceName);
            if (instance != null)
            {
                try {
                    var val = instance.Read(tagName);
                    return $"Tag '{tagName}' = Type: {val.Type}"; // SDataValue struct doesn't expose a simple ToString for value usually, but we'll try!
                } catch (Exception ex) {
                    return $"Failed to read tag: {ex.Message}";
                }
            }
            return $"Instance '{instanceName}' not found.";
        }

        [McpServerTool(Name = "plcsim_refresh_tags"), Description("Refresh the tag list from the simulation")]
        public static string PlcSimRefreshTags([Description("Name of the instance")] string instanceName)
        {
            var instance = SimulationRuntimeManager.CreateInterface(instanceName);
            if (instance != null)
            {
                instance.UpdateTagList();
                return $"Tags refreshed for '{instanceName}'. Total tags: {instance.TagInfos.Length}";
            }
            return $"Instance '{instanceName}' not found.";
        }

        [McpServerTool(Name = "plcsim_run"), Description("Set the simulated PLC to RUN mode")]
        public static string PlcSimRun([Description("Name of the instance")] string instanceName)
        {
            var instance = SimulationRuntimeManager.CreateInterface(instanceName);
            if (instance != null)
            {
                instance.Run();
                return $"Run triggered for '{instanceName}'.";
            }
            return $"Instance '{instanceName}' not found.";
        }

        [McpServerTool(Name = "plcsim_save_profile"), Description("Save the current simulation state as a profile")]
        public static string PlcSimSaveProfile(
            [Description("Name of the instance")] string instanceName,
            [Description("Path to the storage folder")] string storagePath)
        {
            var instance = SimulationRuntimeManager.CreateInterface(instanceName);
            if (instance != null)
            {
                instance.ArchiveStorage(storagePath);
                return $"Profile saved for '{instanceName}' to '{storagePath}'.";
            }
            return $"Instance '{instanceName}' not found.";
        }

        [McpServerTool(Name = "plcsim_set_instance_config"), Description("Configure settings of a PLCSim instance")]
        public static string PlcSimSetInstanceConfig(
            [Description("Name of the instance")] string instanceName,
            [Description("IP Address")] string ipAddress = "",
            [Description("Subnet Mask")] string subnetMask = "255.255.255.0",
            [Description("Default Gateway")] string defaultGateway = "0.0.0.0")
        {
            var instance = SimulationRuntimeManager.CreateInterface(instanceName);
            if (instance != null)
            {
                if (!string.IsNullOrEmpty(ipAddress))
                {
                    var suite = new SIPSuite4
                    {
                        IPAddress = new SIP { IPString = ipAddress },
                        SubnetMask = new SIP { IPString = subnetMask },
                        DefaultGateway = new SIP { IPString = defaultGateway }
                    };
                    instance.SetIPSuite(1u, suite, false);
                }
                return $"Instance '{instanceName}' configured with IP {ipAddress}.";
            }
            return $"Instance '{instanceName}' not found.";
        }

        [McpServerTool(Name = "plcsim_set_network"), Description("Configure network settings for the simulation")]
        public static string PlcSimSetNetwork(
            [Description("Name of the instance")] string instanceName,
            [Description("Network Mode (e.g. TCPIPMultipleAdapter)")] string mode = "TCPIPMultipleAdapter",
            [Description("Interface mapping (e.g. IE1)")] string mapping = "IE1")
        {
            if (Enum.TryParse<ENetworkMode>(mode, out var eMode))
            {
                SimulationRuntimeManager.NetworkMode = eMode;
                
                var instance = SimulationRuntimeManager.CreateInterface(instanceName);
                if (instance != null)
                {
                    if (Enum.TryParse<EPLCInterface>(mapping, out var eMapping))
                    {
                        // Default to the first available virtual switch index or user can specify later
                        instance.SetNetInterfaceMapping(eMapping, 0); 
                        SimulationRuntimeManager.SetNetInterfaceBindings(0);
                        return $"Network set to {mode} and mapped {mapping} for '{instanceName}'.";
                    }
                }
                return $"Instance '{instanceName}' not found or mapping invalid.";
            }
            return $"Invalid network mode '{mode}'.";
        }

        [McpServerTool(Name = "plcsim_set_runtime_config"), Description("Set runtime configuration for the simulation")]
        public static string PlcSimSetRuntimeConfig() { return "Not implemented"; }

        [McpServerTool(Name = "plcsim_set_runtime_port"), Description("Set the runtime communication port")]
        public static string PlcSimSetRuntimePort() { return "Not implemented"; }

        [McpServerTool(Name = "plcsim_set_widget_value"), Description("Set a value on a simulation widget element")]
        public static string PlcSimSetWidgetValue() { return "Not implemented"; }

        [McpServerTool(Name = "plcsim_start_runtime"), Description("Start the PLCSim runtime engine")]
        public static string PlcSimStartRuntime() { return "Not implemented"; }

        [McpServerTool(Name = "plcsim_start_simulation"), Description("Start the PLCSim simulation")]
        public static string PlcSimStartSimulation() { return "Not implemented"; }

        [McpServerTool(Name = "plcsim_status"), Description("Get the overall status of PLCSim")]
        public static string PlcSimStatus() { return "Not implemented"; }

        [McpServerTool(Name = "plcsim_stop"), Description("Set the simulated PLC to STOP mode")]
        public static string PlcSimStop([Description("Name of the instance")] string instanceName)
        {
            var instance = SimulationRuntimeManager.CreateInterface(instanceName);
            if (instance != null)
            {
                instance.Stop();
                return $"Stop triggered for '{instanceName}'.";
            }
            return $"Instance '{instanceName}' not found.";
        }

        [McpServerTool(Name = "plcsim_stop_simulation"), Description("Stop the running PLCSim simulation")]
        public static string PlcSimStopSimulation() { return "Not implemented"; }

        [McpServerTool(Name = "plcsim_update_profile"), Description("Update an existing simulation profile")]
        public static string PlcSimUpdateProfile() { return "Not implemented"; }

        [McpServerTool(Name = "plcsim_write_tag"), Description("Write a value to a single simulation tag")]
        public static string PlcSimWriteTag(
            [Description("Name of the instance")] string instanceName,
            [Description("Tag name")] string tagName,
            [Description("Value to write (must match the tag's data type, e.g. true for Bool)")] string value,
            [Description("Tag data type (e.g. Bool, Int32)")] string dataType = "Bool")
        {
            var instance = SimulationRuntimeManager.CreateInterface(instanceName);
            if (instance != null)
            {
                try {
                    if (Enum.TryParse<EPrimitiveDataType>(dataType, true, out var eType))
                    {
                        var sdata = new SDataValue { Type = eType };
                        switch (eType)
                        {
                            case EPrimitiveDataType.Bool: sdata.Bool = bool.Parse(value); break;
                            case EPrimitiveDataType.Int8: sdata.Int8 = sbyte.Parse(value); break;
                            case EPrimitiveDataType.UInt8: sdata.UInt8 = byte.Parse(value); break;
                            case EPrimitiveDataType.Int16: sdata.Int16 = short.Parse(value); break;
                            case EPrimitiveDataType.UInt16: sdata.UInt16 = ushort.Parse(value); break;
                            case EPrimitiveDataType.Int32: sdata.Int32 = int.Parse(value); break;
                            case EPrimitiveDataType.UInt32: sdata.UInt32 = uint.Parse(value); break;
                            case EPrimitiveDataType.Int64: sdata.Int64 = long.Parse(value); break;
                            case EPrimitiveDataType.UInt64: sdata.UInt64 = ulong.Parse(value); break;
                            case EPrimitiveDataType.Float: sdata.Float = float.Parse(value); break;
                            case EPrimitiveDataType.Double: sdata.Double = double.Parse(value); break;
                            default: return $"Writing for type '{dataType}' is not fully mapped in this stub yet.";
                        }
                        instance.Write(tagName, sdata);
                        return $"Successfully wrote '{value}' to tag '{tagName}'.";
                    }
                    return $"Invalid data type '{dataType}'.";
                } catch (Exception ex) {
                    return $"Failed to write tag: {ex.Message}";
                }
            }
            return $"Instance '{instanceName}' not found.";
        }
    }
}
