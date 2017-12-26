﻿using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using NewLife.Data;
using NewLife.NoDb.Storage;

namespace NewLife.NoDb
{
    /// <summary>数据库</summary>
    /// <remarks>
    /// 一个Server实例可以有多个数据库。
    /// 每个数据库有自己的头部、索引区和数据区，可对应一个磁盘文件。
    /// </remarks>
    public class Database
    {
        #region 属性
        /// <summary>幻数</summary>
        public const String Magic = "NoDb";

        /// <summary>版本</summary>
        public Int32 Version { get; private set; }

        /// <summary>索引区</summary>
        private DbIndex Index { get; set; }

        ///// <summary>数据区</summary>
        //public Heap Heap { get; private set; }

        private MemoryMappedFile _mmf;
        //private MemoryMappedViewAccessor _view;
        #endregion

        #region 构造
        ///// <summary>使用数据流实例化数据库</summary>
        ///// <param name="stream"></param>
        //public Database(Stream stream)
        //{
        //    Read(stream);
        //}

        /// <summary>使用内存映射文件实例化数据库</summary>
        /// <param name="file"></param>
        /// <param name="name"></param>
        public Database(String file, String name = null)
        {
            if (name.IsNullOrEmpty())
            {
                if (!file.IsNullOrEmpty())
                    name = Path.GetFileNameWithoutExtension(file.GetFullPath());
                else
                    name = "NoDb";
            }

            // 多进程加锁，避免同时创建初始化
            var mutex = new Mutex(true, "Mutex_" + name, out var mutexCreated);
            try
            {
                name = "MMF_" + name;
                if (file.IsNullOrEmpty())
                    _mmf = MemoryMappedFile.CreateOrOpen(name, 1024);
                else
                {
                    file = file.GetFullPath();

                    var capacity = 4 * 1024 * 1024;
                    //if (file.AsFile().Exists) capacity = 0;
                    //_mmf = MemoryMappedFile.CreateFromFile(file, FileMode.OpenOrCreate, name, capacity, MemoryMappedFileAccess.ReadWrite);

                    // 使用文件流可以控制共享读写，让别的进程也可以读写文件
                    var fs = new FileStream(file, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.RandomAccess);
                    if (fs.Length == 0) fs.SetLength(1024);
                    _mmf = MemoryMappedFile.CreateFromFile(fs, name, capacity, MemoryMappedFileAccess.ReadWrite, null, HandleInheritability.None, false);
                }

                // 给予非系统账号完全权限
                _mmf.CheckAccessControl();

                using (var fs = _mmf.CreateViewStream())
                {
                    var p = fs.Position;
                    if (!Read(fs))
                    {
                        fs.Position = p;
                        Write(fs);
                    }
                }
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
        #endregion

        #region 主要方法
        /// <summary>获取</summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public Packet Get(String key)
        {
            /*
             * 1，从索引区找到节点信息
             * 2，根据节点信息指向，从数据区读取数据
             */

            if (!TryGetValue(key, out var block)) return null;

            return null;
        }

        /// <summary>尝试获取</summary>
        /// <param name="key"></param>
        /// <param name="block"></param>
        /// <returns></returns>
        public Boolean TryGetValue(String key, out Block block)
        {
            block = null;
            return false;
        }

        /// <summary>设置</summary>
        /// <param name="key"></param>
        /// <param name="pk"></param>
        /// <returns></returns>
        public Boolean Set(String key, Packet pk)
        {
            return false;
        }
        #endregion

        #region 序列化
        private Boolean Read(Stream stream)
        {
            if (stream.Length < 6) return false;

            var magic = stream.ReadBytes(4).ToStr();
            if (Magic != magic) return false;

            // 版本和头部长度
            Version = stream.ReadByte();
            var len = stream.ReadByte();
            if (len < 32 + 4 || stream.Position + len >= stream.Length) return false;

            var buf = stream.ReadBytes(len);
            var ms = new MemoryStream(buf);
            var reader = new BinaryReader(ms);

            var idx = Block.Read(reader);
            var data = Block.Read(reader);

            len = (Int32)ms.Position;
            var crc = reader.ReadUInt32();

            // 校验
            if (crc != buf.ReadBytes(0, len).Crc()) return false;

            // 索引区
            Index = new DbIndex(_mmf, idx);

            // 数据区
            //Heap = new Heap(_mmf, data);

            return true;
        }

        private void Write(Stream stream)
        {
            var writer = new BinaryWriter(stream);

            writer.Write(Magic.GetBytes());

            var v = Version;
            if (v <= 0) v = 1;
            writer.Write((Byte)v);

            //writer.Write((Byte)32);
            var ms = new MemoryStream();
            var writer2 = new BinaryWriter(ms);

            // 索引区
            var idx = new Block(256, 2 * 1024 * 1024);
            Index = new DbIndex(_mmf, idx);
            idx.Write(writer2);

            //// 数据区
            //bk = Heap.GetArea();
            //bk.Write(writer2);

            // 计算校验
            var buf = ms.ToArray();
            var crc = buf.Crc();
            writer.Write((Byte)32);
            writer.Write(buf, 0, buf.Length);
            writer.Write(crc);
        }
        #endregion
    }
}