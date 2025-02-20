using System.Text.Json.Serialization;

namespace Consumer;

/// <summary>
/// Represents the top-level Kafka statistics log.
/// Docs comes form: https://github.com/confluentinc/librdkafka/blob/93877617709eb071a0f4ec7038c54e2764abefc9/STATISTICS.md
/// </summary>
public class KafkaStatisticsDto
{
    public DateTimeOffset StatisticsCreatedAt { get; } = DateTimeOffset.Now;

    /// <summary>
    /// Handle instance name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// The configured (or default) client.id.
    /// </summary>
    [JsonPropertyName("client_id")]
    public string? ClientId { get; set; }

    /// <summary>
    /// Instance type (producer or consumer).
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// librdkafka's internal monotonic clock (microseconds).
    /// </summary>
    [JsonPropertyName("ts")]
    public long Timestamp { get; set; }

    /// <summary>
    /// Wall clock time in seconds since the epoch.
    /// </summary>
    [JsonPropertyName("time")]
    public long Time { get; set; }

    /// <summary>
    /// Time since this client instance was created (microseconds).
    /// </summary>
    [JsonPropertyName("age")]
    public long Age { get; set; }

    /// <summary>
    /// Number of ops (callbacks, events, etc.) waiting in queue for application to serve.
    /// </summary>
    [JsonPropertyName("replyq")]
    public int ReplyQueue { get; set; }

    /// <summary>
    /// Current number of messages in producer queues.
    /// </summary>
    [JsonPropertyName("msg_cnt")]
    public int MessageCount { get; set; }

    /// <summary>
    /// Current total size of messages in producer queues.
    /// </summary>
    [JsonPropertyName("msg_size")]
    public int MessageSize { get; set; }

    /// <summary>
    /// Threshold: maximum number of messages allowed on the producer queues.
    /// </summary>
    [JsonPropertyName("msg_max")]
    public int MessageMax { get; set; }

    /// <summary>
    /// Threshold: maximum total size of messages allowed on the producer queues.
    /// </summary>
    [JsonPropertyName("msg_size_max")]
    public int MessageSizeMax { get; set; }

    /// <summary>
    /// Total number of requests sent to Kafka brokers.
    /// </summary>
    [JsonPropertyName("tx")]
    public int TotalRequestsCount { get; set; }

    /// <summary>
    /// Total number of bytes transmitted to Kafka brokers.
    /// </summary>
    [JsonPropertyName("tx_bytes")]
    public int TransmissionBytes { get; set; }

    /// <summary>
    /// Total number of responses received from Kafka brokers.
    /// </summary>
    [JsonPropertyName("rx")]
    public int Responses { get; set; }

    /// <summary>
    /// Total number of bytes received from Kafka brokers.
    /// </summary>
    [JsonPropertyName("rx_bytes")]
    public int ResponsesBytes { get; set; }

    /// <summary>
    /// Total number of messages transmitted (produced) to Kafka brokers.
    /// </summary>
    [JsonPropertyName("txmsgs")]
    public int TransmittedMessages { get; set; }

    /// <summary>
    /// Total number of message bytes (including framing) transmitted to Kafka brokers.
    /// </summary>
    [JsonPropertyName("txmsg_bytes")]
    public int TransmittedMessageBytes { get; set; }

    /// <summary>
    /// Total number of messages consumed, not including ignored messages.
    /// </summary>
    [JsonPropertyName("rxmsgs")]
    public int ReceivedMessages { get; set; }

    /// <summary>
    /// Total number of message bytes (including framing) received from Kafka brokers.
    /// </summary>
    [JsonPropertyName("rxmsg_bytes")]
    public int ReceivedMessageBytes { get; set; }

    /// <summary>
    /// Internal tracking of legacy vs new consumer API state.
    /// </summary>
    [JsonPropertyName("simple_cnt")]
    public int SimpleCount { get; set; }

    /// <summary>
    /// Number of topics in the metadata cache.
    /// </summary>
    [JsonPropertyName("metadata_cache_cnt")]
    public int MetadataCacheCount { get; set; }

    /// <summary>
    /// Dict of brokers, key is broker name, value is object.
    /// </summary>
    [JsonPropertyName("brokers")]
    public Dictionary<string, BrokerInfo> Brokers { get; set; } = new();

    /// <summary>
    /// Dict of topics, key is topic name, value is object.
    /// </summary>
    [JsonPropertyName("topics")]
    public Dictionary<string, TopicInfo> Topics { get; set; } = new();

    /// <summary>
    /// Consumer group metrics.
    /// </summary>
    [JsonPropertyName("cgrp")]
    public ConsumerGroupInfo? ConsumerGroup { get; set; }
}

public class BrokerInfo
{
    /// <summary>
    /// Broker hostname, port and broker id
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Broker id (-1 for bootstraps)
    /// </summary>
    [JsonPropertyName("nodeid")]
    public int NodeId { get; set; }

    /// <summary>
    /// Broker hostname
    /// </summary>
    [JsonPropertyName("nodename")]
    public string? NodeName { get; set; }

    /// <summary>
    /// Broker state (INIT, DOWN, CONNECT, AUTH, APIVERSION_QUERY, AUTH_HANDSHAKE, UP, UPDATE)
    /// </summary>
    [JsonPropertyName("state")]
    public string? State { get; set; }

    /// <summary>
    /// Total number of requests sent
    /// </summary>
    [JsonPropertyName("tx")]
    public int TotalRequestsCount { get; set; }

    /// <summary>
    /// Total number of responses received
    /// </summary>
    [JsonPropertyName("rx")]
    public int TotalResponsesCount { get; set; }
}

public class TopicInfo
{
    /// <summary>
    /// Topic name
    /// </summary>
    [JsonPropertyName("topic")]
    public string? Topic { get; set; }

    /// <summary>
    /// Partitions dict, key is partition id. See partitions below.
    /// </summary>
    [JsonPropertyName("partitions")]
    public Dictionary<string, PartitionInfo> Partitions { get; set; } = new();
}

public class PartitionInfo
{
    /// <summary>
    /// Partition Id (-1 for internal UA/UnAssigned partition)
    /// </summary>
    [JsonPropertyName("partition")]
    public int Partition { get; set; }

    /// <summary>
    /// The id of the broker that messages are currently being fetched from
    /// </summary>
    [JsonPropertyName("broker")]
    public int Broker { get; set; }

    /// <summary>
    /// Current leader broker id
    /// </summary>
    [JsonPropertyName("leader")]
    public int Leader { get; set; }

    /// <summary>
    /// Number of messages waiting to be produced in first-level queue
    /// </summary>
    [JsonPropertyName("msgq_cnt")]
    public int MessageQueueCount { get; set; }

    /// <summary>
    /// Number of bytes in msgq_cnt
    /// </summary>
    [JsonPropertyName("msgq_bytes")]
    public int MessageQueueBytes { get; set; }

    /// <summary>
    /// Consumer fetch state for this partition (none, stopping, stopped, offset-query, offset-wait, active).
    /// </summary>
    [JsonPropertyName("fetch_state")]
    public string? FetchState { get; set; }

    /// <summary>
    /// Difference between (hi_offset or ls_offset) and committed_offset). hi_offset is used when
    /// isolation.level=read_uncommitted, otherwise ls_offset.
    /// </summary>
    [JsonPropertyName("consumer_lag")]
    public int ConsumerLag { get; set; }
}

public class ConsumerGroupInfo
{
    /// <summary>
    /// Local consumer group handler's state.
    /// </summary>
    [JsonPropertyName("state")]
    public string? State { get; set; }

    /// <summary>
    /// Time elapsed since last rebalance (assign or revoke) (milliseconds).
    /// </summary>
    [JsonPropertyName("rebalance_age")]
    public int RebalanceAge { get; set; }

    /// <summary>
    /// Total number of re-balances (assign or revoke).
    /// </summary>
    [JsonPropertyName("rebalance_cnt")]
    public int RebalanceCount { get; set; }
}