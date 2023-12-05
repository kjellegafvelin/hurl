# Hurl - A simple HTTP load generator

Hurl is used to generate HTTP load against a target, and report on the results. You can use it to test the 
performance of your web applications or api:s.

You can specify the number of runners to use, and the number of requests to make per runner.

## Installation

Open your terminal and run the following command:

```
dotnet tool install --global gp-hurl
```

## Usage

```
USAGE:
    hurl [Url] [OPTIONS]

ARGUMENTS:
    [Url]    The URL to create requests for

OPTIONS:
                     DEFAULT
    -h, --help                  Prints help information
    -v, --version               Prints version information
    -c, --count      1          Number of requests to make per runner
    -r, --runners    1          Number of runners to use in parallel
        --ramp-up               Ramp up time in milliseconds between each runner
```


