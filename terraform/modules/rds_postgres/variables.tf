variable "name" {
  type        = string
  description = "Name identifier for the RDS instance."
}

variable "vpc_id" {
  type        = string
  description = "VPC ID for RDS."
}

variable "subnet_ids" {
  type        = list(string)
  description = "Subnets for RDS subnet group."
}

variable "allowed_security_group_ids" {
  type        = list(string)
  description = "Security groups allowed to access RDS."
}

variable "db_name" {
  type        = string
  description = "Database name."
}

variable "username" {
  type        = string
  description = "Database admin username."
}

variable "password" {
  type        = string
  description = "Database admin password."
}

variable "engine_version" {
  type        = string
  description = "PostgreSQL engine version."
  default     = "18"
}

variable "instance_class" {
  type        = string
  description = "RDS instance class."
  default     = "db.t4g.micro"
}

variable "allocated_storage" {
  type        = number
  description = "Allocated storage in GB."
  default     = 20
}

variable "backup_retention_period" {
  type        = number
  description = "Backup retention period in days."
  default     = 7
}

variable "skip_final_snapshot" {
  type        = bool
  description = "Skip final snapshot on destroy."
  default     = true
}

variable "tags" {
  type        = map(string)
  description = "Tags to apply to RDS resources."
  default     = {}
}

