<!-- TOC -->
* [What is this repo?](#what-is-this-repo)
* [Kafka](#kafka)
  * [Introduction](#introduction)
    * [Sources](#sources)
    * [Events](#events)
    * [Topics](#topics)
    * [Topic partitions](#topic-partitions)
  * [Use cases](#use-cases)
  * [Design choices of Kafka](#design-choices-of-kafka)
      * [Sources](#sources-1)
    * [Motivation](#motivation)
    * [Persistence](#persistence)
      * [Don't fear file systems!](#dont-fear-file-systems)
      * [Efficiency](#efficiency)
      * [End-to-end batch compression](#end-to-end-batch-compression)
    * [The producer](#the-producer)
      * [Load balancing](#load-balancing)
      * [Asynchronous read](#asynchronous-read)
    * [The consumer](#the-consumer)
      * [Push vs. pull](#push-vs-pull)
      * [Consumer position](#consumer-position)
      * [offline data load](#offline-data-load)
      * [Static membership](#static-membership)
    * [Message delivery semantics](#message-delivery-semantics)
      * [Semantics from producer point of view](#semantics-from-producer-point-of-view)
      * [Semantics from the consumer point of view](#semantics-from-the-consumer-point-of-view)
      * [Other](#other)
<!-- TOC -->

# What is this repo?

This repository is my introduction to kafka, my first experience and practice of kafka. This README mostly covers my
understanding of Kafka and the primary source of it is Kafka documentation version `3.9`.

# Kafka

## Introduction

### Sources

* https://kafka.apache.org/documentation/

### Events

Events represents that something happened in the world or in our business. Any form of **read** or **write** from
Kafka is in form of **events**. Events contain a key, value, timestamp and optional header metadata.
Here's an example event:

* Event key: "Alice"
* Event value: "Made a payment of $200 to Bob"
* Event timestamp: "Jun. 25, 2020 at 2:06 p.m."

### Topics

Kafka organizes events in **Topics**. Very simplified, we can view Topics as directories in our system and events
as files in those Topics. Topics in Kafka are always multi-producers and multi-consumers. Each topic can have zero, one,
or many producers and can have zero, one, or many consumers that subscribe to those events. Events in Kafka Topics can
be read as often as needed unlike traditional messaging systems which would delete messages after consumption. Kafka
Topics can be configured to hold messages as long as you want. Kafka performance is basically constant regardless of
how long you retain your events in a Kafka Topic.

### Topic partitions

Each Kafka topic is partitioned, meaning a topic is spread over many "buckets" located on different Kafka brokers.
This placement of partitions is why we can read or write from/to many brokers at the same time. Each event that is
published to a Kafka topic is appended to one of that topic's partitions; Every event with the same key (E.g.:
a customer or vehicle ID) are written to the same topic partition. Kafka guarantees that any consumer of a
topic partition will read the data (events) in the same order as they were written.

To make the data fault-tolerant, each topic can be replicated across multiple geo-regions or datacenters.

## Use cases

TODO

## Design choices of Kafka

#### Sources

* https://kafka.apache.org/documentation/#design

### Motivation

They wanted to create a system where all the real-time data feeds of a system could be handled at a large scale,
this explains some of the design choices that were made.
This system need to fulfil these requirements:

* High throughput
* Low latency message delivery so it can be a replacement to handle more traditional messaging use-cases.
* Handle failure and be resilient and guarantee fault-tolerance in case of system failure.
* It should be partitioned, distributed and have the ability to handle read-time processing of data.

Supporting all the above made Kafka closer to a database log rather than a traditional messaging system.

### Persistence

#### Don't fear file systems!

Disk is much faster and slower than most people think. By using sequential IO operations and some teqniches, Kafka
is able to have a high throughput. To explain what these teqniches are we most first understand how data (events)
are stored within a Kafka topic. Each Kafka topic consists of multiple partitions and each partition is consisting of
one or many logs. Logs are append-only immutable files where events are stored.

Each log have a maximum size allowed (E.g.: 1GB). Each time a log size exceeds the maximum size allowed, a new log file
is created and new events are stored in there. We usually don't want to store and maintain all of our previous events
so we can use Kafka retains configuration to remove old events, but as said previously, logs are immutable. So how
Kafka is going to delete old events you ask? By removing the whole log file! Kafka waits until all the events in a log
file are older that the specified amount, then it can delete the whole log file. This technic allows Kafka to be able
to write events but to avoid the log files being fragmented all across the disk.

Also, because modern operating systems have some sort of disk caching (E.g.: in linux it's called page cache), Kafka
avoids having an internal buffer for caching the data that is read from disk. Operating systems usually use read-ahead
and write-behind to improve the performance of disk usage and Kafka is using this fact to increase its throughput. How
you may ask? That's a great question.
Because Kafka log files are immutable and append-only file structures, and because most of the time all consumers of
a partition want the latest events, these events will end up on the OS page cache. Because of that, OS does not need
to use a IO operation to get the latest events, instead it can serve them from the main memory, and this is why read
speed of Kafka can be as fast as possible.

#### Efficiency

Because of how Kafka handles IO operations, disk access pattern have been eliminated. But we still have two more
inefficiency to resolve:

* Too many small I/O operations
* Excessive byte copying

To avoid having too many small I/O (E.g.: disk or network operations), Kafka will batch messages before any operations
that includes publishers sending messages to brokers, brokers storing those messages and consumers request messages
in batch.

Byte copying at low message rate will not be an issue but at the scale of the Kafka throughput, it will. To avoid this
Kafka uses the same format for sending events over the network and storing them, this means that publishers, brokers
and consumers all use a common structure. And because of that, Kafka can use zero-copy approach to send events via the
network. To explain what is **zero-copy**, first we need to explain how a typical sending a file over the network looks
like:

* The operating system reads data from the disk into page cache in kernel space.
* The application reads the data from kernel space into a user-space buffer.
* The application writes the data back into kernel space into a socket buffer.
* The operating system copies the data from the socket buffer to the NIC buffer where it is sent over the network.

But with **zero-copy** approach, we can eliminate the application from the above using **sendfile** in Linux. In
**sendfile** Linux will read data from disk into page cache and send that directly into the NIC buffer, thus
eliminating memory buffers needed in application layer.

This combination of page cache and zero-copy approach means that when all consumers of a topic are mostly caught
up, we will see no read activity on disk. This is because once the recent events are requested from disk, they
will be cached by the operating system on page cache and the following requests will be fulfilled using OS page cache.
Also because of the zero-copy approach (and page cache), message consumption rate can reach the limit of the
network connection.

> TLS/SSL libraries work at the user space (in-kernel `SSL_sendfile` is currently not supported by Kafka 3.9). Due
> to this restriction, `sendfile` is not used when SSL is enabled. For enabling SSL configuration, refer to
> `security.protocol` and `security.inter.broker.protocol` configuration.

#### End-to-end batch compression

In some cases the bottleneck is not CPU or disk speed, its network bandwidth. This is specially true in cases of when
need to send data between datacenters. Kafka does support compressing messages but the problem is that compressing
algorithms usually work better when there is more data to be compressed at least in lossless data compression algorithms
like `gzip`. What that means is that mostly, compressing a batch of messages can reduce more than if those same messages are
compressed one by one. Kafka producers can compress the batch messages send to Kafka. The broker will decompress the batch
message to validate it's content (E.g.: validate the number of messages in a batch is the same as what that batch header states),
but will **NOT** alter or decompress the batch message that is sent from the producer when storing them to the log file or when
kafka is sending events to consumers.

### The producer

#### Load balancing

Every `topic` contains multiple `partitions`, each partition has a single leader, this means that a topic with `n` amount
of partition will have `n` amount of leaders, one **leader** per partition. The producer sends data directly to the broker
that is the **leader** for that topic partition. To help producers locate which broker is the leader of that partition,
every node in a Kafka cluster can answer a request for `metadata` about which servers are alive and where each leader of a
topic partition is located at any given time.

The client control which partition it sends messages to. This can happen in random, a simple load balancing of sort. There
is also semantic partitioning function, to use it we have to simply use the same key for our events; Let me explain further.
When a event key is provided, Kafka will hash that key and map it to a partition. To use this functionality, we need to
publish events of a same resource with the same key, E.g.: use `user_ID` for publishing events about a user. Using this
mechanism allows for the use of data locality as well. Let's imagine if we used `user_ID` as our key, then at this point
every event of that topic with the same `user_ID` will end up in the **same partition** and because each partition can **only
have a single consumer**, the data required to process that event such as `user_name`, `user_first_name` can be cached on
that server because it is likely that this consumer is going to process another event for that `user_ID` in the near future.
**Please note that** we don't **need** to cache those additional data that is given in the example above. But because I
wanted to illustrate the point of what is data locality, I created that scenario where we can cache that user's data in our
consumer.

#### Asynchronous read

In Kafka, we can almost always use batching to get better throughput such as in the producers. We can configure our producers
to accumulate more bytes (AKA batch events) before sending them to the broker. The batching can be configured to wait for
a fixed number of messages and to wait no longer than a fixed latency bound (say 64K and 10ms). Batching is configurable
and give a mechanism to trade off a small amount of latency for better throughput.

### The consumer

The Kafka consumer works by issuing a `fetch` request to the brokers leading the partitions it wants to consumer. The consumer
sends its offset in the log with each request and receives back a chunk of messages to be processed, this mechanism allows
for consumers to rewind back and re-consume if needed.

#### Push vs. pull

An initial question was to answer the question of "should consumers `pull` events from brokers or should the broker `push`
the events to the consumer?". Answers to this question have pros and cons. Kafka chose a more traditional approach shared
by most messaging systems, where data is `pushed` by the publishers to the brokers and the data is `pulled` by the consumers
from the broker. The reason being is throughput.

Generally the goal is for the consumer to consume at the maximum possible rate; But in the case of messages being `pushed`
to consumers, the broker has to handle the difficult task to push to divers consumers, each with their own `consume rate`.
This approach tends to overwhelm consumers by broker sending more event that can be consumed at any given time.

A `pull-based` system has the nicer property where each consumer can consume at their maximum possible rate, even if a consumer
falls behind, it can simply catch up when it can. Another advantage is that a pull-based system lends itself to aggressive
batching of data sent to the consumer. A `push-based` system must choose between low latency and high throughput. If tuned
for **low-latency** the broker will send messages one at a time, each time a message arrives, it is pushed to the consumer
only to be buffered in the consumer anyway.
The `pull-based` approach fixes this as the consumer always pulls all the available events after its position in the log
(or up to a configurable amount). So one gets maximum throughput without adding unnecessary latency.

Kafka also supports **long pulling** method. This method mitigates the situation where there is no more events available
in the brokers that are not consumed, but the consumer is stuck in a tight loop asking the broker for new data. To fix this,
one can use `long-pulling` approach, to allow the broker to block the consumer request for data until new events arrives
(and optionally waiting until a given number of bytes are available to ensure large transfer sizes AKA batching).

We can imagine another scenario where Kafka `pull`s data end-to-end. But this also introduce additional complexity such as
the producers now have to have a local log that they append to and brokers need to pull from them, but this means in use
cases where we have many producers we have to replicate and operate possibly thousands of logs. another reason for Kafka
to not use this approach is, and I quote (Kafka docs V3.9):
> Our experience running persistent data systems at scale led us to feel that involving thousands of disks in the system
> across many applications would not actually make things more reliable and would be a nightmare to operate. And in practice
> we have found that we can run a pipeline with strong SLAs at large scale without a need for producer persistence.

#### Consumer position

Keeping track of what has been consumed, surprisingly, is a key performance point in messaging systems. Most systems keep
metadata about what messages are consumed by each consumer. Keep tracking of what messages has been consumed can get very
tricky. For example, a system can mark an `event` as **consumed** when it is sent to the consumer, but in the event of a
network failure or any other failure, that message can be lost before reaching the consumer, or it has reached the consumer but
the consumer crashes or fails to process that event. Because that `event` is set as **consumed** on the broker, it is
never going to be sent out to other consumers, meaning this event is lost forever.

To fix this issue, many systems wait for an `acknowledgment` from the consumer before marking the event as `consumed`. But
this can get tricky too, because now the system has two states, `sent` and `acknowledged`. This solution fixes our problem
but creates more! Let's imagine a scenario where a message is sent and processed by the consumer, but it fails to send the
`acknowledgement` to the broker, and because of that, the broker will re-send that event, and it will be re-consumed. The
second problem is about performance. The system now needs to keep multiple status for each message (first to mark it as
`sent` to not send it again and then mark it as `consumed` so it can be removed). Tricky problems must be dealt with, like
what to do with messages that are sent but never acknowledged.

Kafka handles this differently. Each topic in Kafka is divided into `partitions`. Each partition can only be consumed a
single consumer within each subscribing group at any given time. This means that the **position** of the consumer in each
partition is only a **single number** which makes the state of what has been consumed very small, just an integer for each
partition. This `position` can be periodically checkpointed, this makes the equivalent of message acknowledgement very cheap.

This approach has another nice benefit. Now the consumers can **rewind** back and reconsume messages if needed. This violates
the common contract of a queue, but turns out to be an essential feature for many consumers. For example, if a consumer
code has a bug, and it is discovered after consuming some messages, the code can be fixed and the consumer now can **rewind**
and re-consume those messages now that the bug has been fixed.

#### offline data load

Scalable persistence allows for the possibility of consumers that only periodically consume data such as batch data loads
that periodically bulk-load data into an offline system such as Apache Hadoop or a relational data warehouse.

In case of Hadoop we parallelize the data load by splitting over individual map tasks, one for each node/topic/partition
combination, allowing full parallelism in the loading. Hadoop provides task management, and tasks which fail can restart
without danger of duplicate data, they simply restart from their original position.

#### Static membership

Static membership aims to improve the availability of stream applications, consumer groups and other applications built
on top of the group `re-balance protocol`. The re-balance protocol relies on the group coordinator to allocate entity IDs
to group members. These generated IDs are **ephemeral** and will change each time a members restart and rejoin. For consumer
based applications, this "dynamic membership" can cause a large percentage of task re-assigned to different instances during
administrative operations such as code deploy, configuration updates or periodic restarts. Motivated by this observation,
Kafka's group management protocol allows group member to provide persistant entity IDs. Group membership remains unchanged
based on those ids, thus no re-balance will be triggered.

To use this feature, the broker and the client need to be on version 2.3 or beyond and the configuration for static membership
should be set. If the broker is on an older version than 2.3, but you configured Kafka to use static membership, the application
will detect the broker version and then throws an `UnsupportedException`. If you accidentally configured duplicate ids
for different instances, a fencing mechanism on the broker side will inform your duplicate client to shut down immediately
by triggering a `org.apache.kafka.common.errors.FencedInstanceIdException`.

### Message delivery semantics

Kafka provides some guarantees for consumers and publishers for message delivery. There are multiple possible message delivery
semantics that can be provided:

* *At most once*: Messages maybe lost but are **never** redelivered.
* *At least once*: Messages are never lost but **may** be redelivered.
* *Exactly once*: This is what most people want, messages are delivered **once and only once**.

We can break down delivery semantics guarantees into two sections, delivery guarantees for **publishers** and **consumers**.

Many systems claim to provide `exactly once` delivery semantics, but it is important to read the fine print, most of these claims
are misleading (i.e. they don't to the case where the publisher or consumer can fail, cases where there are multiple consumer
processes, or cases where the data written to the disk can be lost).

#### Semantics from producer point of view

Kafka's semantics are straight forward. When publishing a message we have a notion of the message being "commited" to the log. Once
a message is *commited* it will never be lost as long as one broker that replicates the partition to which the message was written in
remains "alive".
> The definition of **commited messages**, **alive partition**, and what type of **failure** Kafka attempts to handle will be described in more detail in
> the next section. For now lets assume a perfect **lossless** broker and try to understand the guarantees to the publishers and consumers.

If a producer attempts to send a message and experiences a network error, it can not be sure if this error happened after message was
commited in the broker or before that. Prior to `0.11.0.0` the producer had no choice but to resend that message. This provides at least
once semantic delivery since the message could have been commited but because of a network error, the producer did not receive "ack" and
will resend the message leading to duplicate event in the log. Since `0.11.0.0`, the Kafka producer also supports an idempotent delivery
option which guarantees resending will not result in duplicate events in the log.
To Achieve this, every producer is assigned an ID by the broker and deduplicates messages using a sequence number sent by the producer
along with every message. Also, beginning with `0.11.0.0` the producer supports the ability to send messages to multiple topic partitions
in a transaction-like semantics: i.e. either all messages are successfully written or none of them are. The main use case for this is
**exactly once** delivery semantic between Kafka topics.

Not all use cases require such strong guarantees. The producer can specify the desired level of durability. For example, it can choose
to not wait for any acknowledgement from broker or wait for an acknowledgement of message was only written to that partition but have not
been replicated, or it can wait for the message to be written to log and replicated across brokers.

#### Semantics from the consumer point of view

Now let's describe the semantics from the consumer point of view. All replicas have the exact same log with the same offsets. The consumer
controls its offset in the log. If the consumer never crashed, it could have stored its offset into memory. But if the consumer fails, we
want that partition to be taken over by another process, this required the new process to choose an appropriate offset. Let's say a consumer
reads some messages, it has now several options for processing messages and updating its position:

1. It can read the messages, save its new position to the log and then start processing messages. In this case there is a possibility that
   the consumer saves its new position but fails or crashes **before** it can process the messages or save the result of the processing. Then
   the new process that took over that partition would start at the new updated position that the previous process saved even though few messages
   before that position are in fact **not processed**. This translates to *at most once* delivery semantic.
2. It can read the messages, process them and then save its position into the log. In this case there is a possibility that the consumer successfully
   processes and saves the result of those events but **fails** to update its new position in the log, which means that when a new process takes over the
   partition, it will start reading the messages that are already **processed** by the preivous consumer. This translates to *at least once* delivery
   semantic.

#### Other

So what about **exactly once delivery** semantic? When consuming from a topic and producing to another topic (as in a Kafka streams application), we can
use the transaction capabilities in `0.11.0.0` that were mentioned. The consumer's position is saved as a message in a topic, so we write the offset to
the Kafka in ths same transaction as the output topics receiving the processed data. If the transaction is **aborted**, the consumer position is reverted
back to its original position and the produced data on the output topic will not be visible to other consumers depending on their **isolation level**. In
default `read_uncommited` isolation level, all messages are visible to consumers even if they were part of an aborted transaction, but in the `read_commited`,
the consumers will only see messages from transactions which are *commited* (and any messages which were **NOT** part of a transaction).

When writing to an external system, the limitation is to coordinate the consumer position and with what is actually stored as the output. The classic way of
handling this would be to introduce a *two phase commit* between the storage of the consumer position and the storage of the consumer output. But this can
be handled more simply and generally by letting the consumer store its offset in the same place as its output. This is better because some of the systems
that the consumer would want to write to can possibly **NOT** support a two phase commit. As an example of this, consider a Kafka connector which populates
data in `HDFS` along with consumer position in the log, now it is guaranteed that either both the position and the data is updated or neither is.

So effectively, Kafka supports exactly-once semantics in Kafka streams, and transactional producer/consumer can be used generally to provide exactly-once
delivery when transferring and processing data between Kafka topics. Exactly-once delivery for other destination systems require cooperation with such
system, but Kafka provides the offset which makes implementing this feasible (see also Kafka connect). Otherwise, Kafka guarantees *at-least-once* delivery
by *default*, and allows users to implement *at-most-once* delivery by disabling retries on the producer and updating consumer position before prior to
processing a batch of messages.
