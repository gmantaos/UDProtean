﻿using System;

using UDProtean;
using UDProtean.Client;
using UDProtean.Server;
using ChanceNET;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

using NUnit;
using NUnit.Framework;

namespace UDProtean.Tests
{
	public class TestBehavior : Server.UDPClientBehavior
	{
		uint expected = 0;

		protected override void OnClose()
		{
		}

		protected override void OnMessage(byte[] data)
		{
			uint num = BitConverter.ToUInt32(data, 0);
			Assert.AreEqual(expected++, num);

			Console.WriteLine("Received {0} bytes of data", data.Length);

			Send(data);
		}

		protected override void OnError(Exception ex)
		{
		}

		protected override void OnOpen()
		{
		}
	}
}
