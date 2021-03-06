﻿using System;

using UDProtean;
using ChanceNET;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;

using NUnit;
using NUnit.Framework;
using System.Threading;

namespace UDProtean.Tests
{
	[TestFixture]
	public class SequentialTest : TestBase
	{

		[Test]
		public void Utils()
		{
			byte[] b1 = chance.Hash(DatagramLength());
			byte[] b2 = chance.Hash(DatagramLength());

			byte[] joined = b1.Append(b2);

			Assert.AreEqual(b1.Length + b2.Length, joined.Length);

			CollectionAssert.AreEqual(b1, joined.Slice(0, b1.Length));
			CollectionAssert.AreEqual(b2, joined.Slice(b1.Length, b2.Length));

			CollectionAssert.AreEqual(b1, b1.ToLength(b1.Length));
		}
		
		public void Sending()
		{
			byte[][] buffer = TestBuffer();
			uint expected = 0;

			SequentialCommunication comm;

			SendData send = (data) =>
			{
				byte[] expectedBuffer = buffer[expected];

				Assert.AreEqual(expectedBuffer.Length, data.Length - SequentialCommunication.SequenceBytes);

				byte[] sequence = data.Slice(0, SequentialCommunication.SequenceBytes).ToLength(4);
				uint sequenceNum = BitConverter.ToUInt32(sequence, 0);

				Assert.AreEqual(expected % SequentialCommunication.SEQUENCE_SIZE, BitConverter.ToUInt32(sequence, 0));


				data = data.Slice(SequentialCommunication.SequenceBytes);
				CollectionAssert.AreEqual(expectedBuffer, data);
			};

			comm = new SequentialCommunication(send, null);
			
			foreach (byte[] dgram in buffer)
			{
				comm.Send(dgram);
				expected++;
			}
		}

		[Test]
		public void Receiving()
		{
			SequentialCommunication comm;

			uint next = 0;

			DataCallback callback = (data) =>
			{
				uint received = BitConverter.ToUInt32(data, 0);

				Assert.AreEqual(next, received);
				next++;
			};

			comm = new SequentialCommunication(null, callback);

			Func<uint, byte[], byte[]> genDgram = (seq, data) =>
			{
				byte[] sequence = BitConverter.GetBytes(seq)
										  .ToLength(SequentialCommunication.SequenceBytes);

				return sequence.Append(0).Append(data);
			};

			Action<uint, uint> send = (seq, data) =>
			{
				byte[] dgram = genDgram(seq, BitConverter.GetBytes(data).ToLength(4));
				comm.Received(dgram);
			};

			send(0, 0);
			send(1, 1);
			send(2, 2);
			send(5, 5);
			send(3, 3);
			send(1, 1);
			send(4, 4);
		}

		[Test]
		public void ReceivingFragmented()
		{
			SequentialCommunication comm;

			uint next = 0;

			DataCallback callback = (data) =>
			{
				Debug(data);

				uint received = BitConverter.ToUInt32(data, 0);

				Console.WriteLine(received);

				Assert.AreEqual(next, received);
				next++;
			};

			comm = new SequentialCommunication(null, callback);

			Func<uint, byte, byte[], byte[]> genDgram = (seq, frag, data) =>
			{
				byte[] sequence = BitConverter.GetBytes(seq)
										  .ToLength(SequentialCommunication.SequenceBytes);

				byte[] toSend = sequence.Append(frag).Append(data);

				Debug(toSend);

				return toSend;
			};

			Action<uint, byte, uint> send = (seq, frag, data) =>
			{
				byte[] dgram = genDgram(seq, frag, BitConverter.GetBytes(data).ToLength(4));
				comm.Received(dgram);
			};
			
			send(0, 0, 0);
			send(1, 1, 1);
			send(3, 1, 2);
            send(5, 0, 3);
            send(4, 0, 0);
            send(2, 0, 0);

            Assert.AreEqual(4, next);
        }

		[TestCase(0.0)]
		[TestCase(0.1)]
		[TestCase(0.2)]
		public void Communicating(double packetLoss)
		{
			Queue<uint> vals = new Queue<uint>(chance.N(1000, () => (uint)chance.Natural()));
			Queue<uint> toSend = new Queue<uint>(vals);

			SequentialCommunication comm1 = null;			
			SequentialCommunication comm2 = null;

			Action<SequentialCommunication, byte[]> trySend = (comm, data) =>
			{
				if (chance.Bool(1 - packetLoss))
				{
					new Thread(() =>
					{
						Thread.Sleep(20);
						comm.Received(data);
					}).Start();
				}
			};

			Action<uint, byte[]> verify = (expected, data) =>
			{
				uint recv = BitConverter.ToUInt32(data.ToLength(4), 0);
				Assert.AreEqual(expected, recv);
			};

			SendData send1 = (data) =>
			{
				trySend(comm2, data);
			};

			SendData send2 = (data) =>
			{
				trySend(comm1, data);
			};

			DataCallback callback2 = (data) =>
			{
				verify(vals.Dequeue(), data);
			};

			comm1 = new SequentialCommunication(send1, null);
			comm2 = new SequentialCommunication(send2, callback2);

			for (int i = 0; i < vals.Count; i++)
			{
				byte[] data = BitConverter.GetBytes(toSend.Dequeue());
				comm1.Send(data);
			}
		}


		public void Fragmentation(double packetLoss)
		{
			byte[][] buffer = TestBuffer(size: 10, datagramMin: 1000, datagramMax: 5000);

			int exp = 0;
			SequentialCommunication comm1 = null;
			SequentialCommunication comm2 = null;

			Action<SequentialCommunication, byte[]> trySend = (comm, data) =>
			{
				if (chance.Bool(1 - packetLoss))
				{
					new Thread(() =>comm.Received(data)).Start();
				}
			};

			DataCallback callback = (data) =>
			{
				CollectionAssert.AreEqual(buffer[exp++], data);
			};

			SendData send1 = (data) =>
			{
				trySend(comm2, data);
			};

			SendData send2 = (data) =>
			{
				trySend(comm1, data);
			};

			comm1 = new SequentialCommunication(send1, null);
			comm2 = new SequentialCommunication(send2, callback);

			for (int i = 0; i < buffer.Length; i++)
			{
				byte[] data = buffer[i];
				comm1.Send(data);
			}
		}
	}
}
