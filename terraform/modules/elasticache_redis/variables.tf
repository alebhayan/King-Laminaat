variable "name" {
  type        = string
  description = "Name identifier for the Redis replication group."
}

variable "vpc_id" {
  type        = string
  description = "VPC ID."
}

variable "subnet_ids" {
  type        = list(string)
  description = "Subnets for ElastiCache."
}

variable "allowed_security_group_ids" {
  type        = list(string)
  description = "Security groups allowed to access Redis."
}

variable "engine_version" {
  type        = string
  description = "Redis engine version."
  default     = "7.0"
}

variable "node_type" {
  type        = string
  description = "Node instance type."
  default     = "cache.t4g.micro"
}

variable "number_cache_clusters" {
  type        = number
  description = "Number of cache nodes."
  default     = 1
}

variable "automatic_failover_enabled" {
  type        = bool
  description = "Enable automatic failover."
  default     = false
}

variable "multi_az_enabled" {
  type        = bool
  description = "Enable Multi-AZ."
  default     = false
}

variable "snapshot_retention_limit" {
  type        = number
  description = "Days to retain snapshots."
  default     = 7
}

variable "snapshot_window" {
  type        = string
  description = "Snapshot window."
  default     = "03:00-04:00"
}

variable "maintenance_window" {
  type        = string
  description = "Maintenance window."
  default     = "sun:05:00-sun:06:00"
}

variable "tags" {
  type        = map(string)
  description = "Tags to apply to Redis resources."
  default     = {}
}

