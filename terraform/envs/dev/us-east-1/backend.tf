terraform {
  backend "s3" {
    bucket = "fsh-state-bucket"
    key    = "dev/us-east-1/terraform.tfstate"
    region = "us-east-1"
  }
}

