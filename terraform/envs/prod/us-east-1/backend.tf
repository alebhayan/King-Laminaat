terraform {
  backend "s3" {
    bucket = "CHANGE_ME-terraform-state"
    key    = "prod/us-east-1/terraform.tfstate"
    region = "us-east-1"
  }
}

