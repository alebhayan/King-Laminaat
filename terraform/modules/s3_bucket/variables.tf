variable "name" {
  type        = string
  description = "Bucket name."
}

variable "tags" {
  type        = map(string)
  description = "Tags to apply to the bucket."
  default     = {}
}

