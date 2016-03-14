﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ValveResourceFormat.Blocks;

namespace ValveResourceFormat
{
    /// <summary>
    /// Represents a Valve resource.
    /// </summary>
    public class Resource : IDisposable
    {
        private const ushort KnownHeaderVersion = 12;

        private FileStream FileStream;

        /// <summary>
        /// Gets the binary reader. USE AT YOUR OWN RISK!
        /// It is exposed publicly to ease of reading the same file.
        /// </summary>
        /// <value>The binary reader.</value>
        public BinaryReader Reader { get; private set; }

        /// <summary>
        /// Gets the resource size.
        /// </summary>
        public uint FileSize { get; private set; }

        /// <summary>
        /// Gets the version of this resource, should be 12.
        /// </summary>
        public ushort HeaderVersion { get; private set; }

        /// <summary>
        /// Gets the file type version.
        /// </summary>
        public ushort Version { get; private set; }

        /// <summary>
        /// Gets the list of blocks this resource contains.
        /// </summary>
        public Dictionary<BlockType, Block> Blocks { get; }

        /// <summary>
        /// Gets or sets the type of the resource.
        /// </summary>
        /// <value>The type of the resource.</value>
        public ResourceType ResourceType { get; set; }

        /// <summary>
        /// Gets the ResourceExtRefList block.
        /// </summary>
        public ResourceExtRefList ExternalReferences
        {
            get
            {
                Block block;
                Blocks.TryGetValue(BlockType.RERL, out block);
                return (ResourceExtRefList)block;
            }
        }

        /// <summary>
        /// Gets the ResourceEditInfo block.
        /// </summary>
        public ResourceEditInfo EditInfo
        {
            get
            {
                Block block;
                Blocks.TryGetValue(BlockType.REDI, out block);
                return (ResourceEditInfo)block;
            }
        }

        /// <summary>
        /// Gets the ResourceIntrospectionManifest block.
        /// </summary>
        public ResourceIntrospectionManifest IntrospectionManifest
        {
            get
            {
                Block block;
                Blocks.TryGetValue(BlockType.NTRO, out block);
                return (ResourceIntrospectionManifest)block;
            }
        }

        /// <summary>
        /// Gets the Vertex and Index Buffer block.
        /// </summary>
        public VBIB VBIB
        {
            get
            {
                Block block;
                Blocks.TryGetValue(BlockType.VBIB, out block);
                return (VBIB)block;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Resource"/> class.
        /// </summary>
        public Resource()
        {
            ResourceType = ResourceType.Unknown;
            Blocks = new Dictionary<BlockType, Block>();
        }

        /// <summary>
        /// Releases binary reader.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (FileStream != null)
                {
                    FileStream.Dispose();
                    FileStream = null;
                }

                if (Reader != null)
                {
                    Reader.Dispose();
                    Reader = null;
                }
            }
        }

        /// <summary>
        /// Reads the given <see cref="Stream"/>.
        /// </summary>
        /// <param name="input">The input <see cref="Stream"/> to read from.</param>
        public void Read(Stream input)
        {
            Reader = new BinaryReader(input);

            FileSize = Reader.ReadUInt32();

            if (FileSize == Package.MAGIC)
            {
                throw new InvalidDataException("Use Package() class to parse VPK files.");
            }

            // TODO: Some real files seem to have different file size
            if (FileSize != Reader.BaseStream.Length)
            {
                //throw new Exception(string.Format("File size does not match size specified in file. {0} != {1}", FileSize, Reader.BaseStream.Length));
            }

            HeaderVersion = Reader.ReadUInt16();

            if (HeaderVersion != KnownHeaderVersion)
            {
                throw new InvalidDataException(string.Format("Bad header version. ({0} != expected {1})", HeaderVersion, KnownHeaderVersion));
            }

            Version = Reader.ReadUInt16();

            var blockOffset = Reader.ReadUInt32();
            var blockCount = Reader.ReadUInt32();

            Reader.BaseStream.Position += blockOffset - 8; // 8 is 2 uint32s we just read

            for (var i = 0; i < blockCount; i++)
            {
                var blockType = Encoding.UTF8.GetString(Reader.ReadBytes(4));
                var block = ConstructFromType(blockType);

                var position = Reader.BaseStream.Position;

                // Offset is relative to current position
                block.Offset = (uint)position + Reader.ReadUInt32();
                block.Size = Reader.ReadUInt32();

                block.Read(Reader, this);

                Blocks.Add(block.GetChar(), block);

                switch (block.GetChar())
                {
                    case BlockType.REDI:
                        // Try to determine resource type by looking at first compiler indentifier
                        if (ResourceType == ResourceType.Unknown && EditInfo.Structs.ContainsKey(ResourceEditInfo.REDIStruct.SpecialDependencies))
                        {
                            var specialDeps = (Blocks.ResourceEditInfoStructs.SpecialDependencies)EditInfo.Structs[ResourceEditInfo.REDIStruct.SpecialDependencies];

                            if (specialDeps.List.Count > 0)
                            {
                                ResourceType = DetermineResourceTypeByCompilerIdentifier(specialDeps.List[0].CompilerIdentifier);
                            }
                        }

                        break;

                    case BlockType.NTRO:
                        if (ResourceType == ResourceType.Unknown && IntrospectionManifest.ReferencedStructs.Count > 0)
                        {
                            switch (IntrospectionManifest.ReferencedStructs[0].Name)
                            {
                                case "VSoundEventScript_t":
                                    ResourceType = ResourceType.SoundEventScript;
                                    break;

                                case "CWorldVisibility":
                                    ResourceType = ResourceType.WorldVisibility;
                                    break;
                            }
                        }

                        break;
                }

                Reader.BaseStream.Position = position + 8;
            }
        }

        /// <summary>
        /// Opens and reads the given filename.
        /// </summary>
        /// <param name="filename">The file to open and read.</param>
        public void Read(string filename)
        {
            FileStream = new FileStream(filename, FileMode.Open, FileAccess.Read);

            Read(FileStream);
        }

        private Block ConstructFromType(string input)
        {
            switch (input)
            {
                case "DATA":
                    return ConstructResourceType();

                case "REDI":
                    return new ResourceEditInfo();

                case "RERL":
                    return new ResourceExtRefList();

                case "NTRO":
                    return new ResourceIntrospectionManifest();

                case "VBIB":
                    return new VBIB();
            }

            throw new ArgumentException(string.Format("Unrecognized block type '{0}'", input));
        }

        private ResourceData ConstructResourceType()
        {
            switch (ResourceType)
            {
                case ResourceType.Panorama:
                    return new ResourceTypes.Panorama();

                case ResourceType.Sound:
                    return new ResourceTypes.Sound();

                case ResourceType.Texture:
                    return new ResourceTypes.Texture();

                case ResourceType.SoundEventScript:
                    return new ResourceTypes.SoundEventScript();

                case ResourceType.Particle:
                    return new ResourceTypes.BinaryKV3();

                case ResourceType.Mesh:
                    if (Version == 0)
                    {
                        break;
                    }

                    return new ResourceTypes.BinaryKV3();
            }

            if (Blocks.ContainsKey(BlockType.NTRO))
            {
                return new ResourceTypes.NTRO();
            }

            return new ResourceData();
        }

        private static ResourceType DetermineResourceTypeByCompilerIdentifier(string identifier)
        {
            if (identifier.StartsWith("Compile", StringComparison.Ordinal))
            {
                identifier = identifier.Remove(0, "Compile".Length);
            }

            // Special mappings and otherwise different identifiers
            switch (identifier)
            {
                case "Psf": return ResourceType.ParticleSnapshot;
                case "AnimGroup": return ResourceType.AnimationGroup;
                case "VPhysXData": return ResourceType.PhysicsCollisionMesh;
                case "Font": return ResourceType.BitmapFont;
                case "RenderMesh": return ResourceType.Mesh;
            }

            ResourceType resourceType;

            if (Enum.TryParse(identifier, false, out resourceType))
            {
                return resourceType;
            }

            return ResourceType.Unknown;
        }
    }
}
