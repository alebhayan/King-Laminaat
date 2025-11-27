variable "name" {
  type        = string
  description = "Name of the ALB."
}

variable "subnet_ids" {
  type        = list(string)
  description = "Subnets for the ALB."
}

variable "security_group_id" {
  type        = string
  description = "Security group for the ALB."
}

variable "tags" {
  type        = map(string)
  description = "Tags to apply to ALB resources."
  default     = {}
}

