﻿using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using AElf.Kernel;
using AElf.Kernel.Node;
using AElf.Network;
using AElf.Network.Connection;
using AElf.Network.Data;
using AElf.Network.Peers;
using AElf.Node.Protocol.Events;
using Google.Protobuf;
using NLog;

namespace AElf.Node
{
    public class P2P : IP2P
    {
        private readonly ILogger _logger;
        private readonly INetworkManager _netManager;

        private BlockingCollection<NetMessageReceivedEventArgs> _messageQueue =
            new BlockingCollection<NetMessageReceivedEventArgs>();

        private P2PHandler _handler;

        public P2P(P2PHandler handler, ILogger logger, INetworkManager netManager)
        {
            _handler = handler;
            _logger = logger;
            _netManager = netManager;
            _netManager.MessageReceived += ProcessPeerMessage;
        }

        public async Task ProcessLoop()
        {
            try
            {
                while (true)
                {
                    var args = _messageQueue.Take();

                    var message = args.Message;
                    var msgType = (AElfProtocolMsgType) message.Type;

                    if (msgType == AElfProtocolMsgType.RequestBlock)
                    {
                        await HandleBlockRequest(message, args.PeerMessage);
                    }
                    else if (msgType == AElfProtocolMsgType.TxRequest)
                    {
                        await HandleTxRequest(message, args.PeerMessage);
                    }
                }
            }
            catch (Exception e)
            {
                _logger?.Trace(e, "Error while dequeuing.");
            }
        }

        internal async Task HandleBlockRequest(Message message, PeerMessageReceivedArgs args)
        {
            try
            {
                var breq = BlockRequest.Parser.ParseFrom(message.Payload);
                var block = await _handler.GetBlockAtHeight(breq.Height);
                Message req = NetRequestFactory.CreateMessage(AElfProtocolMsgType.Block, block.ToByteArray());
                if (message.HasId)
                    req.Id = message.Id;

                args.Peer.EnqueueOutgoing(req);

                _logger?.Trace("Send block " + block.GetHash().ToHex() + " to " + args.Peer);
            }
            catch (Exception e)
            {
                _logger?.Trace(e, "Error while during HandleBlockRequest.");
            }
        }

        private async Task HandleTxRequest(Message message, PeerMessageReceivedArgs args)
        {
            try
            {
                TxRequest breq = TxRequest.Parser.ParseFrom(message.Payload);

                TransactionList txList = new TransactionList();
                foreach (var txHash in breq.TxHashes)
                {
                    var hash = txHash.ToByteArray();
                    var tx = await _handler.GetTransaction(hash);
                
                    if(tx != null)
                        txList.Transactions.Add(tx);
                }

                byte[] serializedTxList = txList.ToByteArray();
                Message req = NetRequestFactory.CreateMessage(AElfProtocolMsgType.Transactions, serializedTxList);
                
                if (message.HasId)
                {
                    req.HasId = true;
                    req.Id = message.Id;
                }
                
                args.Peer.EnqueueOutgoing(req);

//todo                _logger?.Trace("Send tx " + t.GetHash().ToHex() + " to " + args.Peer + "(" + serializedTxList.Length +
//                               " bytes)");
            }
            catch (Exception e)
            {
                //_logger?.Trace(e, $"Transaction request failed. Hash : {hash}");
            }
        }

        private void ProcessPeerMessage(object sender, EventArgs e)
        {
            if (sender != null && e is NetMessageReceivedEventArgs args && args.Message != null)
            {
                _messageQueue.Add(args);
            }
        }

        public async Task<bool> BroadcastBlock(IBlock block)
        {
            if (!(block is Block b))
            {
                return false;
            }

            var serializedBlock = b.ToByteArray();
            await _netManager.BroadcastBock(block.GetHash().Value.ToByteArray(), serializedBlock);

            var bh = block.GetHash().ToHex();
            _logger?.Trace(
                $"Broadcasted block \"{bh}\" to peers with {block.Body.TransactionsCount} tx(s). Block height: [{block.Header.Index}].");

            return true;
        }
    }
}