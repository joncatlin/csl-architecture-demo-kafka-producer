﻿using System;
using System.Collections.Generic;
using System.Text;
using Akka.Actor;
using Confluent.Kafka.Serialization;
using Confluent.Kafka;
using Newtonsoft.Json;
using StatsdClient;

namespace kafka_actors
{
    #region Command classes
    // The message to be published
    public class Publish
    {
        public Publish(string key, object msg)
        {
            this.Key = key;
            this.Msg = msg;
        }

        public string Key { get; private set; }
        public object Msg { get; private set; }
    }

    // If a message fails then it is contained in the Resend cmd below
    public class Resend
    {
        public Resend(string key, string msg)
        {
            this.Key = key;
            this.Msg = msg;
        }

        public string Key { get; private set; }
        public string Msg { get; private set; }
    }
    #endregion

    class KafkaPublisherActor : ReceiveActor
    {
        Producer<string, string> producer;
        private readonly string _topicName;
        ActorSelection thisActor = null;


        public KafkaPublisherActor(string topicName, Producer<string, string> producer, string actorName)
        {
            this.producer = producer;
            _topicName = topicName;

            // Save a reference to this actor for later use in producer callback
            this.thisActor = Context.ActorSelection("/user/" + actorName);

            // Commands
            Receive<Publish>(cmd => PublishMsg(cmd));
            Receive<Resend>(cmd => ResendMsg(cmd));
        }


        // Send a message that has not been converted to json
        private void PublishMsg(Publish cmd)
        {
            // Convert the msg to JSON before sending
            string json = JsonConvert.SerializeObject(cmd.Msg);
            Send(cmd.Key, json);

            // Increment a counter by 1
            DogStatsd.Increment("PublishMsg");
        }


        // Resends a msg, presumably after a failure event
        private void ResendMsg(Resend cmd)
        {
            Send(cmd.Key, cmd.Msg);

            // Update telemetry
            DogStatsd.Increment("ResendMsg");
        }


        public void Send(string msgKey, string json)
        {
            try
            {
                var deliveryReport = producer.ProduceAsync(_topicName, msgKey, json);
                deliveryReport.ContinueWith(task =>
                {
/*
                    if (task.Result.Error.HasError)
                    {
                        // Update telemetery
                        DogStatsd.Increment("send-ERROR");

                        // TODO Check for the type of error. Local timeout is certainly one that can occur, in which case resend. Check others as 
                        // may be a resend is a bad thing.
                        thisActor.Tell(new Resend(msgKey, json));

                    }
*/
                    
                    if (task.IsCompletedSuccessfully)
                    {
                        DogStatsd.Increment("send-SUCCESS");
                        var key = task.Result.Key;
                        if (key.EndsWith("999999"))
                        {
                            Console.WriteLine($"Complete key: {task.Result.Key}");
                        }
                        else if (key.EndsWith("0000"))
                        {
                            Console.WriteLine($"Sent key: {task.Result.Key}");
                        }
                    }
                    //                Console.WriteLine($"Partition: {task.Result.Partition}, Offset: {task.Result.Offset}");
                });
            } catch (Exception e)
            {
                Console.WriteLine($"Exception in Send: {e.StackTrace}");
            }
        }
    }
}









