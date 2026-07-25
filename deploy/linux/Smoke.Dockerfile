FROM ubuntu:24.04

ARG DEBIAN_FRONTEND=noninteractive

RUN apt-get update \
    && apt-get install --yes --no-install-recommends \
        libfontconfig1 \
        libice6 \
        libsm6 \
        libx11-6 \
        xauth \
        xvfb \
    && rm -rf /var/lib/apt/lists/*
