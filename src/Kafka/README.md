# Kafka

<!-- TOC -->
* [Kafka](#kafka)
  * [Introduction](#introduction)
    * [Sources](#sources)
    * [Events](#events)
    * [Topics](#topics)
    * [Topic partitions](#topic-partitions)
  * [Use cases](#use-cases)
  * [Design choices of Kafka](#design-choices-of-kafka)
      * [Sources](#sources-)
    * [Motivation](#motivation)
    * [Persistence](#persistence)
      * [Don't fear file systems!](#dont-fear-file-systems)
      * [Efficiency](#efficiency)
      * [End-to-end batch compression](#end-to-end-batch-compression)
<!-- TOC -->

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
