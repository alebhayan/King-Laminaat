variable "name" {
  type        = string
  description = "Name prefix for networking resources."
}

variable "cidr_block" {
  type        = string
  description = "CIDR block for the VPC."
}

variable "public_subnets" {
  description = "Map of public subnet definitions."
  type = map(object({
    cidr_block = string
    az         = string
  }))
}

variable "private_subnets" {
  description = "Map of private subnet definitions."
  type = map(object({
    cidr_block = string
    az         = string
  }))
}

variable "tags" {
  type        = map(string)
  description = "Tags to apply to networking resources."
  default     = {}
}

