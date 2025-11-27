variable "name" {
  type        = string
  description = "Name of the ECS service."
}

variable "region" {
  type        = string
  description = "AWS region."
}

variable "cluster_arn" {
  type        = string
  description = "ARN of the ECS cluster."
}

variable "container_image" {
  type        = string
  description = "Container image to deploy."
}

variable "container_port" {
  type        = number
  description = "Container port exposed by the service."
}

variable "cpu" {
  type        = string
  description = "Fargate CPU units."
}

variable "memory" {
  type        = string
  description = "Fargate memory in MiB."
}

variable "desired_count" {
  type        = number
  description = "Desired number of tasks."
  default     = 1
}

variable "vpc_id" {
  type        = string
  description = "VPC ID for the service."
}

variable "vpc_cidr_block" {
  type        = string
  description = "CIDR block of the VPC."
}

variable "subnet_ids" {
  type        = list(string)
  description = "Subnets for ECS tasks."
}

variable "assign_public_ip" {
  type        = bool
  description = "Assign public IP to tasks."
  default     = false
}

variable "listener_arn" {
  type        = string
  description = "ALB listener ARN."
}

variable "listener_rule_priority" {
  type        = number
  description = "Priority for the ALB listener rule."
}

variable "path_patterns" {
  type        = list(string)
  description = "Path patterns for ALB listener rule."
  default     = ["/*"]
}

variable "health_check_path" {
  type        = string
  description = "Health check path for the target group."
  default     = "/"
}

variable "log_retention_in_days" {
  type        = number
  description = "CloudWatch log retention in days."
  default     = 30
}

variable "environment_variables" {
  type        = map(string)
  description = "Plain environment variables for the container."
  default     = {}
}

variable "tags" {
  type        = map(string)
  description = "Tags to apply to resources."
  default     = {}
}
