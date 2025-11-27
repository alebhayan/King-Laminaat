variable "region" {
  type        = string
  description = "AWS region where the state bucket is created."
}

variable "bucket_name" {
  type        = string
  description = "Name of the S3 bucket for Terraform remote state."
}

