﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;

using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("UDProtean.Tests")]

namespace UDProtean.Shared
{
	internal delegate void SendData(byte[] data);
	internal delegate void DataCallback(byte[] data);

    internal class SequentialCommunication
    {
		internal const uint SEQUENCE_SIZE = 512;

		public static int SequenceBytes
		{
			get
			{
				int bytes = 0;
				while (Math.Pow(2, bytes * 8) < SEQUENCE_SIZE)
					bytes++;
				return bytes;
			}
		}

		Sequence sending;
		Sequence receiving;
		Sequence ack;

		byte[][] sendingBuffer;
		byte[][] receivingBuffer;

		SendData sendData;
		DataCallback callback;

		public SequentialCommunication(SendData sendData, DataCallback callback)
		{
			this.sendData = sendData;
			this.callback = callback;

			sendingBuffer = new byte[SEQUENCE_SIZE][];
			receivingBuffer = new byte[SEQUENCE_SIZE][];
		}

		public void Send(byte[] data)
		{
			byte[] sequence = BitConverter.GetBytes(sending.Value)
										  .ToLength(SequenceBytes);

			byte[] dgram = sequence.Append(data);

			/*
			 * First, store the datagram in the buffer
			 */
			sendingBuffer[sending.Value] = dgram;
			sending.MoveNext();

			SendData(dgram);
		}

		public void Received(byte[] dgram)
		{
			byte[] sequence = dgram.Slice(0, SequenceBytes).ToLength(4);
			uint sequenceNum = BitConverter.ToUInt32(sequence, 0);

			/*
			 * If the datagram is only SequenceBytes long, then it's an ACK
			 */
			if (dgram.Length == SequenceBytes)
			{
				ProcessAck(sequenceNum);
				return;
			}

			byte[] data = dgram.Slice(SequenceBytes);
			receivingBuffer[sequenceNum] = data;

			if (receiving.Value == sequenceNum)
			{
				/*
				 * This datagram is on-par with the sequence
				 * Handle it
				 */

				// Advance the receiving sequence according to the receiving buffer
				ProcessReceivingBuffer();

				// Send ack
				SendAck(receiving.Previous);
			}
			else
			{
				/*
				 * We received a datagram with an unexpected sequence number
				 * Let the other end know which was the last datagram we received
				 */
				
				SendAck(receiving.Previous);
			}
		}
		
		void ProcessReceivingBuffer()
		{
			while (receivingBuffer[receiving.Value] != null)
			{
				byte[] data = receivingBuffer[receiving.Value];
				receivingBuffer[receiving.Value] = null;
				
				OnData(data);

				receiving.MoveNext();
			}
		}

		void ProcessAck(uint sequenceNum)
		{
			if (ack.Value == sequenceNum)
			{
				Sequence bufferClear = ack.Clone();
				ack.MoveNext();

				while (sendingBuffer[bufferClear.Value] != null 
					&& bufferClear.Value != ack.Value)
				{
					sendingBuffer[bufferClear.Value] = null;
					bufferClear.MovePrevious();
				}

			}
			else
			{
				/*
				 * We received an ACK for a datagram that was not the last on the sequence
				 * Resend the datagram that are after the one being acknowledged
				 */
				uint toRepeat = (sequenceNum + 1) % SEQUENCE_SIZE;
				byte[] dgramToRepeat = sendingBuffer[toRepeat];

				ack.Set(toRepeat);

				if (dgramToRepeat != null)
				{
					SendData(dgramToRepeat);
				}
			}
		}

		void SendAck(uint sequenceNum)
		{
			byte[] ack = BitConverter.GetBytes(sequenceNum).ToLength(SequenceBytes);
			SendData(ack);
		}

		protected virtual void SendData(byte[] data)
		{
			sendData?.Invoke(data);
		}

		protected virtual void OnData(byte[] data)
		{
			callback?.Invoke(data);
		}
    }
}
